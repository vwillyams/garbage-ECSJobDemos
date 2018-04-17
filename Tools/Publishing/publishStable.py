import glob
import os
import subprocess
import shutil
import sys
import argparse
import json
import tarfile
import tempfile
import urllib2
from fnmatch import fnmatch
from inspect import currentframe, getframeinfo
from BumpVersion import BumpVersion
import time
import semver

args = argparse.Namespace()
source_branch = ""
local_packages = {}
modified_packages = {}
best_view_registry = None

def get_packages_folder():
    # type: () -> str
    path = args.packages_path
    if os.path.isfile(path + "manifest.json"):
        return os.path.abspath(path)
    raise Exception("Unable to find {0}".format(path))

def get_list_of_packages(folder):
    # type: (str) -> [str]
    packages = []
    for f in glob.glob(os.path.join(folder, "**/package.json")):
        packages.append(os.path.dirname(f))
    return packages

def parse_package_json(package_path):
    with open("{0}/package.json".format(package_path), 'r') as f:
        return json.load(f)

def compare_json_keys(local_package, installed_package):
    key_diff = set(local_package.keys()) - set(installed_package.keys())
    if len(key_diff) > 0:
        print "    The one package.json files do not have the same keys. The keys missing is:"
        for key in key_diff:
            print "      {0}".format(key)
        return False
    return True

def compare_package_files(local_path, installed_path, current_version):
    print "  Comparing package files locally with published:"

    local_package = parse_package_json(local_path)
    installed_package = parse_package_json(installed_path)

    local_package["version"] = current_version

    if not compare_json_keys(local_package, installed_package):
        return False

    for key, value in local_package.iteritems():
        if value != installed_package[key]:
            # If we are using --add-package-as-dependency-to-package then the dependencies will differ
            # so we do an exception here and check that the ones we are missing are the ones we have flagged
            # and the version we can find on the registry is the same
            if key == "dependencies":
                should_fail = False
                for nested_key, nested_value in installed_package[key].iteritems():
                    if nested_key in value and nested_value == value[nested_key]:
                        continue
                    if nested_key not in value and \
                            any(nested_key in d for d in args.add_package_as_dependency_to_package) and \
                            nested_key in local_packages and \
                            nested_value == local_packages[nested_key]:
                        continue
                    should_fail = True
                if not should_fail:
                    continue

            print "    {0} is differing between packages:".format(key)
            print "      Local: {0}".format(value)
            print "      Published: {0}".format(installed_package[key])
            return False

    print "    Looks the same"
    return True

def _get_all_files_in_package(path):
    files = [os.path.relpath(os.path.join(dp, f), path) for dp, dn, fn in os.walk(os.path.expanduser(path)) for f in fn]
    files.remove("package.json")
    files = [f for f in files if "node_modules" not in f]
    return files

def get_package_from_url(tar_url, file_path): # pragma: no cover
    max_retries = 10
    attempt = 0
    retry_delay = 0.1
    while attempt < max_retries:
        attempt += 1
        try:
            response = urllib2.urlopen(tar_url)
            with open(file_path, 'wb') as f:
                f.write(response.read())
            return True
        except:
            if attempt < max_retries:
                print "  Got exception while downloading {0}. Retrying in {0}sec. (Attempt {1}/{2}".format(tar_url,
                                                                                                           retry_delay,
                                                                                                           attempt,
                                                                                                           max_retries)
                time.sleep(retry_delay)
                retry_delay = retry_delay * 2
            else:
                print "  Reached maximum retries"
                raise

def download_package_tarball(package_name, current_version): # pragma: no cover
    print "Downloading {0} from {1} to see if we have changed anything".format(package_name, best_view_registry)
    tar_url = npm_cmd("view {0}@{1} dist.tarball".format(package_name, current_version), best_view_registry).strip()
    download_path = tempfile.gettempdir()
    print "  Getting tarball from {0} and saving to {1}".format(tar_url, download_path)
    file_path = os.path.join(download_path, "{0}.{1}.tar.gz".format(package_name, current_version))
    if os.path.exists(file_path):
        os.remove(file_path)

    if get_package_from_url(tar_url, file_path):
        return file_path

    raise Exception("Something went wrong with the download of the tarball.")

def get_current_package_extracted(package_name, current_version): # pragma: no cover
    file_path = download_package_tarball(package_name, current_version)
    print "  Extracting tarball"
    download_path = os.path.join(os.path.dirname(file_path), "{0}.{1}".format(package_name, current_version))
    if os.path.exists(download_path):
        shutil.rmtree(download_path)
    tar = tarfile.open(file_path, "r:gz")
    tar.extractall(download_path)
    tar.close()
    # installed_path = os.path.abspath(os.path.join("node_modules", package_name))
    return  os.path.join(download_path, "package")

def is_package_changed(package_folder, package_name, current_version):
    # type: (str) -> bool

    download_path = get_current_package_extracted(current_version, package_name)
    print "Comparing files between {0} and {1}".format(download_path, package_folder)

    package_files = _get_all_files_in_package(download_path)
    repo_files = _get_all_files_in_package(os.path.abspath(package_folder))

    mismatching_files = list(set(package_files).symmetric_difference(set(repo_files)))

    if len(mismatching_files) > 0:
        print "  The number of files don't match. {0} mismatching files".format(len(mismatching_files))
        return True

    match, mismatch, errors = cmp_directories_ignore_line_endings(package_folder, download_path, repo_files)

    if len(mismatch) == 0 and len(errors) == 0:
        if compare_package_files(package_folder, download_path, current_version):
            print "Nothing has changed"
            return False
        else:
            print "{0}/package.json and {1}/package.json are not the same".format(package_folder,
                                                                                  download_path)
            return True
    print "  The following files have changed compared to the currently published package:"
    for m in mismatch:
        print "    {0}".format(m)
    return True
    pass

def cmp_directories_ignore_line_endings(first, second, common_files):
    match = []
    mismatch = []
    errors = []

    for common in common_files:
        first_path = os.path.join(first, common)
        second_path = os.path.join(second, common)
        if not os.path.exists(first_path):
            mismatch.append(common)
            continue
        if not os.path.exists(second_path):
            mismatch.append(common)
            continue

        if not cmp_files(first_path, second_path):
            mismatch.append(common)
            continue
        match.append(common)

    return match, mismatch, errors

def cmp_files(f1, f2):
    line1 = line2 = ' '
    with open(f1, 'r') as f1, open(f2, 'r') as f2:
        while line1 != '' and line2 != '':
            line1 = f1.readline().rstrip("\n\r")
            line2 = f2.readline().rstrip("\n\r")
            if line1 != line2:
                return False
    return True

def is_preview(version_split):
    if version_split.prerelease:
            preview_split = version_split.prerelease.split('.')
            return len(preview_split) == 2 and preview_split[0].startswith('preview') and preview_split[1].isdigit()
    return False

def validate_version(version):
    try:
        version_split = semver.parse_version_info(version)
        if version_split.prerelease and not is_preview(version_split):
            raise ValueError

    except ValueError as ve:
        frameinfo = getframeinfo(currentframe())
        print "Invalid Version Format: ", frameinfo.filename, frameinfo.lineno
        raise ve

def increase_version(version, bumpFlag):
    validate_version(version)

    new_version = version

    if bumpFlag == BumpVersion.RELEASE:
        version_split = semver.parse_version_info(version)
        new_version = semver.format_version(version_split.major, version_split.minor, version_split.patch)

    elif bumpFlag == BumpVersion.PATCH:
        new_version = semver.bump_patch(new_version)
        bumpFlag = BumpVersion.PREVIEW

    elif bumpFlag == BumpVersion.MINOR:
        new_version = semver.bump_minor(new_version)
        bumpFlag = BumpVersion.PREVIEW

    elif bumpFlag == BumpVersion.MAJOR:
        new_version = semver.bump_major(new_version)
        bumpFlag = BumpVersion.PREVIEW

    if bumpFlag == BumpVersion.PREVIEW:
        if not "preview" in version:
            new_version = semver.bump_patch(new_version)
        new_version = semver.bump_prerelease(new_version, 'preview')

    return new_version

def _get_version_in_registry(package_name, registry):
    try:
        version = npm_cmd("view {0} version".format(package_name), registry).strip()

    # Hack for sinopia which returns 404 when a package doesn't exist, whereas no other registry does
    except subprocess.CalledProcessError as e:
        if "is not in the npm registry" in e.output:
            version = ""
        else:
            raise e
    return version

def is_local_package(package_name): # pragma: no cover
    return os.path.isdir("{0}/{1}".format(args.packages_path, package_name))

def get_package_version(package_name):
    # type: (str) -> str

    global best_view_registry
    print "Getting current published package version for {0}".format(package_name)
    highest_version = "0.0.0"
    view_registry_locked = False
    new_registry = None
    if best_view_registry is not None:
        view_registry_locked = True
    highest_version_trimmed = highest_version.split('.')
    for registry in args.view_registries:
        version = _get_version_in_registry(package_name, registry)
        if not version:
            # Package didn't exist in the registry
            continue
        trimmed_version = version.strip().split('+')[0].split('.')
        for i in range(0, 3):
            if int(trimmed_version[i]) < int(highest_version_trimmed[i]):
                continue
            if int(trimmed_version[i]) == int(highest_version_trimmed[i]) and registry != args.publish_registry:
                continue
            highest_version = version.strip()
            highest_version_trimmed = trimmed_version
            new_registry = registry

    # TODO: Do we need something like this? Is there any other way to validate so nothing goes wrong here
    #  if view_registry_locked and new_registry != best_view_registry:
    #    raise Exception("A previous package already selected {0} as the best view registry, but now {1} "
    #                    "wanted to select {2} as the best one since a higher version of that package was "
    #                    "found there ({3}). This needs to be investigated because this shouldn't "
    #                    "happen".format(best_view_registry, package_name, new_registry, highest_version))

    best_view_registry = new_registry

    print "{0} was selected as the best registry to read from since it had the highest package version for {1} ({2})" \
        .format(best_view_registry, package_name, highest_version)

    return highest_version


def publish_new_package(package_name, version):
    # type: (str, str) -> None
    previous_cwd = os.getcwd()
    try:

        os.chdir("{0}/{1}".format(args.packages_path, package_name))
        print "Packing as version {0}".format(version)
        package_archive = npm_cmd("pack .", None).strip()
        print "Publishing {0} to {1}".format(package_archive, args.publish_registry)
        npm_cmd("publish {0}".format(package_archive), args.publish_registry)
        os.remove(package_archive)
    finally:
        os.chdir(previous_cwd)
    pass


def _modify_manifest_registry():
    modified = False
    with open("{0}/manifest.json".format(args.packages_path), 'r') as f:
        manifest = json.load(f)

    if 'registry' not in manifest or (manifest['registry'] != args.publish_registry):
        manifest['registry'] = args.publish_registry
        modified = True

    if modified:
        with open("{0}/manifest.json".format(args.packages_path), 'w') as outfile:
            json.dump(manifest, outfile, indent=4)


def _modify_manifest_for_package(package_name, version):
    with open("{0}/manifest.json".format(args.packages_path), 'r') as f:
        manifest = json.load(f)

    dependencies = {}
    if "dependencies" in manifest:
        dependencies = manifest["dependencies"]
    modified = False
    for key, value in dependencies.iteritems():
        if key != package_name:
            continue
        modified = True
        manifest["dependencies"][package_name] = version

    if not modified and args.add_packages_to_manifest and package_name in args.add_packages_to_manifest:
        print "Adding {0} to the manifest.json".format(package_name)
        manifest["dependencies"][package_name] = version
        modified = True

    if modified:
        with open("{0}/manifest.json".format(args.packages_path), 'w') as outfile:
            json.dump(manifest, outfile, indent=4)


def _modify_package_file_dependencies(package_name):
    """
    We check all modified packages and see if the current package has a dependency to it. If so then we update the
    version
    """
    with open("{0}/{1}/package.json".format(args.packages_path, package_name), 'r') as f:
        package_file = json.load(f)
    modified = False
    dependencies = {}
    if "dependencies" in package_file:
        dependencies = package_file["dependencies"]

    # if we have added the --add-package-as-dependency-to-package we see if we need to add these dependencies here
    manual_dependencies = [dep.replace("{0}:".format(package_name), "")
                           for dep in args.add_package_as_dependency_to_package
                           if dep.startswith("{0}:".format(package_name))]

    for modified_package_name, modified_version in local_packages.iteritems():
        if modified_package_name in dependencies or modified_package_name in manual_dependencies:
            package_file["dependencies"][modified_package_name] = modified_version
            modified = True

    if modified:
        with open("{0}/{1}/package.json".format(args.packages_path, package_name), 'w') as outfile:
            json.dump(package_file, outfile, indent=4)


def _modify_package_version(package_name, version):
    previous_cwd = os.getcwd()
    try:
        os.chdir("{0}/{1}".format(args.packages_path, package_name))
        npm_cmd("--no-git-tag-version version {0}".format(version), None).strip()
    finally:
        os.chdir(previous_cwd)


def modify_json(package_name, version):
    if package_name in modified_packages:
        _modify_package_version(package_name, version)
    _modify_manifest_registry()
    _modify_manifest_for_package(package_name, version)
    _modify_package_file_dependencies(package_name)


def scatter_manifest():
    # Since we might have multiple unity projects in the same repo that should have the same manifest, we find them
    # all and update them
    shared_manifest = os.path.normpath(os.path.join(args.packages_path, "manifest.json"))
    for root, d, f in os.walk('.'):
        for path in f:
            p = os.path.join(root, path)
            if fnmatch(p, "**/Packages/manifest.json") and os.path.normpath(args.packages_path) not in p:
                print "Replacing manifest in {0} with {1}".format(p, shared_manifest)
                shutil.copy(shared_manifest, p)
                git_cmd("add {0}".format(p))
    pass


def strip_unwanted():
    if not args.strip_from_commit:
        return
    for path in args.strip_from_commit:
        for p in glob.glob(path):
            if os.path.isdir(p):
                print "Tried to remove folder {0}".format(p)
                shutil.rmtree(p)
            elif os.path.isfile(p):
                print "Tried to remove file {0}".format(p)
                os.remove(p)


def sanity_check_files():
    hidden_paths = []
    for root, d, f in os.walk('.'):
        for path in d:
            p = os.path.join(root, path)
            if path.startswith(".") and ".git" != path:
                if args.whitelist_hidden_paths and path in args.whitelist_hidden_paths:
                    continue
                hidden_paths.append(p)
        for path in f:
            p = os.path.join(root, path)
            if path.startswith("."):
                if args.whitelist_hidden_paths and path in args.whitelist_hidden_paths:
                    continue
                hidden_paths.append(p)

    if len(hidden_paths) > 0:
        print "There are hidden paths in the repo that aren't white listed. Failing run. Please use " \
              "--whitelist-hidden-paths if you want these paths in:"
        for p in hidden_paths:
            print "  {0}".format(p)
        raise Exception("Unwanted file found in repo")


def squash_commits():
    # We don't actually squash it since we just want whatever is in the source branch right now and add that as a new
    # commit to the target branch. So we delete everything in our publishing branch and then checkout everything
    # from the source branch and commit anything that changed.
    removed = []
    for d in os.listdir("."):
        if ".git" in d:
            continue
        print "Deleting {0}".format(d)
        if os.path.isdir(d):
            shutil.rmtree(d)
        else:
            os.remove(d)
            removed.append(d)

    for d in removed:
        git_cmd("rm {0}".format(d))

    git_cmd("checkout {0} -- .".format(source_branch))

    strip_unwanted()

    sanity_check_files()

    git_cmd("add -A")

    git_cmd("commit -m \"Current squash\" --allow-empty")
    pass


def create_commit():
    # type: () -> bool

    print "Committing changes since merge"
    exists = git_cmd_code_only("fetch target {0}".format(args.target_branch))

    if exists != 0:
        git_cmd("commit -m \"Release 1\" --amend")
        return True

    previous_releases = git_cmd("log target/{0} --pretty=%B".format(args.target_branch)).strip().split('\n')
    next_release = ""
    for previous_release in previous_releases:
        if not previous_release.startswith("Release "):
            continue
        split = previous_release.split(" ")
        if len(split) != 2:
            continue
        if not split[1].isdigit():
            continue

        next_release = "Release {0}".format(int(split[1]) + 1)
        break

    git_cmd("commit -m \"{0}\" --amend --allow-empty".format(next_release))

    # Checking if the last two commits in this branch has any changes between them. If no then we shouldn't push
    anything_changed = git_cmd("diff @~..@")
    if not anything_changed.strip():
        print "Nothing has changed at all. Won't push commit"
        return False
    return True


def git_cmd(cmd):
    formatted_cmd = "git {0}".format(cmd)
    print "  Running: {0}".format(formatted_cmd)
    return subprocess.check_output(formatted_cmd, shell=True, stderr=subprocess.STDOUT)


def git_cmd_code_only(cmd):
    formatted_cmd = "git {0}".format(cmd)
    print "  Running: {0}".format(formatted_cmd)
    with open(os.devnull, 'w') as devnull:
        return subprocess.call(formatted_cmd, shell=True, stderr=devnull, stdout=devnull)


def npm_cmd(cmd, registry):
    registry_cmd = ''
    if registry:
        registry_cmd = "--registry {0}".format(registry)

    formatted_cmd = 'npm {0} {1}'.format(cmd, registry_cmd)

    print "  Running: {0}".format(formatted_cmd)
    return subprocess.check_output(formatted_cmd, shell=True, stderr=subprocess.STDOUT)


def add_destination_repo():
    print "Adding {0} as remote 'target' to repo".format(args.target_repo)
    remote = git_cmd("remote")

    if "target" in remote:
        remote_url = git_cmd("config --get remote.target.url").strip()
        if remote_url != args.target_repo:
            git_cmd("remote rm target")
            remote = ""

    if "target" not in remote:
        git_cmd("remote add target {0}".format(args.target_repo))

    git_cmd("fetch target")
    branches = git_cmd("ls-remote --heads target")
    local_branches = git_cmd("branch")

    # Lets just always remove it so we stay clean
    if "publishStable-temp" in local_branches:
        git_cmd("checkout {0}".format(source_branch))
        git_cmd("branch -D publishStable-temp")

    if "refs/heads/{0}".format(args.target_branch) in branches:
        git_cmd("checkout -b publishStable-temp --track target/{0}".format(args.target_branch))
    else:
        git_cmd("checkout --orphan publishStable-temp")


def process_package(package_path, package_name, root_clone):
    current_package_version = get_package_version(package_name)
    changed = is_package_changed(package_path, package_name, current_package_version)
    if changed is True:
        if args.dry_run:
            print "The package {0} has been changed but --dry-run has been set so it will not get published" \
                .format(package_name)
        else:
            print "The package has been modified since latest published version. A new version of {0} will be " \
                  "published".format(package_name)
        if args.only_publish_existing_packages:
            print "--only-publish-existing-packages is set but we found modification for {0} that we wanted to push. " \
                  "Failing run ".format(package_name)
            raise Exception("Tried to publish modified packages when --only-publish-existing-packages was set")
        new_package_version = increase_version(current_package_version, False, False, False, True)
        local_packages[package_name] = new_package_version
        modified_packages[package_name] = new_package_version
        modify_json(package_name, new_package_version)
    else:
        print "No change detected in the repo. The current version of {0} ({1}) will be used in the project" \
            .format(package_name, current_package_version)
        local_packages[package_name] = current_package_version
        published_version = _get_version_in_registry(package_name, args.publish_registry)
        if published_version != current_package_version:
            print "The version of this package does not exist on the publish registry, so will publish anyway with " \
                  "the current version "
            modified_packages[package_name] = current_package_version
        modify_json(package_name, current_package_version)

    os.chdir(root_clone)
    print ''.ljust(80, '#')

def publish_modified_packages(): # pragma: no cover
    for package_name, version in modified_packages.iteritems():
        if not args.dry_run:
            publish_new_package(package_name, version)

def remove_package_folders(): # pragma: no cover
    if args.dry_run:
        return
    for package_name, version in local_packages.iteritems():
        shutil.rmtree("./{0}/{1}".format(args.packages_path, package_name))

def get_version_from_manifest(package_name): # pragma: no cover
    with open("{0}/manifest.json".format(args.packages_path), 'r') as f:
        manifest = json.load(f)

    if package_name not in manifest["dependencies"]:
        raise Exception(
            "Tried to parse {0} from the manifest.json, but it could not be found. Failing run".format(package_name))

    return manifest["dependencies"][package_name]

def get_registry_from_manifest(): # pragma: no cover
    with open("{0}/manifest.json".format(args.packages_path), 'r') as f:
        manifest = json.load(f)

    return manifest["registry"]

def get_filtered_dependencies_from_view_registry(package_name, package_version):
    dependencies = {}
    result_string = npm_cmd("view {0}@{1} dependencies".format(package_name, package_version),
                            best_view_registry).strip().replace("'", '"')
    j = json.loads(result_string)
    tracked_dependencies = [n.split(":")[1] for n in args.add_package_as_dependency_to_package]
    for key, value in j.iteritems():
        if key not in tracked_dependencies:
            continue
        dependencies[key] = value
    return dependencies

def main():     # pragma: no cover
    global source_branch
    root_dir = os.getcwd()
    repo_dir = args.source_repo
    os.chdir(repo_dir)

    # Just a nice sanity check so we refuse to run if this script has modifications in the repo we are running from.
    # While changing this script you will need to setup another local repo to run it against.
    output = git_cmd("ls-files -m")
    if os.path.basename(__file__) in output:
        print "You are not allowed to run this script against the repo this script resides in if the file has " \
              "modifications, since these would be lost. "
        print "Please use a secondary repo locally while testing this out."
        sys.exit(-1)
    try:
        source_branch = git_cmd("rev-parse --abbrev-ref HEAD").strip()
        print "Will now start creating packages for publish from {0} to {1}:{2}" \
            .format(source_branch, args.target_repo, args.target_branch)
        root_clone = os.getcwd()
        add_destination_repo()

        squash_commits()
        packages_folder = args.packages_path
        packages = get_list_of_packages(packages_folder)
        if len(packages) > 0:
            late_process_packages = []

            for package_path in packages:
                package_name = os.path.basename(os.path.normpath(package_path))
                print "### Package Found: {0} in {1}".format(package_name, package_path).ljust(80, '#')

                if package_path not in late_process_packages and args.add_packages_to_manifest is not None \
                        and package_name in args.add_packages_to_manifest:
                    print "Skipping for now since it is to be added to the manifest at the end"
                    late_process_packages.append(package_path)
                    continue
                else:
                    local_dependencies = [d for d in args.add_package_as_dependency_to_package if
                                          "{0}:".format(package_name) in d]
                    should_defer = False
                    for dep in [l.split(":")[1] for l in local_dependencies]:
                        if dep not in local_packages:
                            print "This package has extra dependencies that haven't been processed yet so we add this " \
                                  "package to late processing. The dependency is: {0}".format(dep)
                            late_process_packages.append(package_path)
                            should_defer = True
                            break
                    if should_defer:
                        continue

                process_package(package_path, package_name, root_clone)

            for package_path in late_process_packages:
                package_name = os.path.basename(os.path.normpath(package_path))
                print "### Package Found to add to manifest: {0} in {1}".format(package_name, package_path).ljust(80,
                                                                                                                  '#')
                process_package(package_path, package_name, root_clone)

            publish_modified_packages()
            remove_package_folders()

        elif args.only_publish_existing_packages:
            print "No packages found in {0}. Will look at --add-package-as-dependency-to-package and " \
                  "--add-packages-to-manifest ".format(args.packages_path)
            if args.view_registries is not None:
                raise Exception("When only republishing packages the --view-registries will be what is already in the "
                                "manifest.json file. Remove all --view-registries and try again")
            if not args.add_package_as_dependency_to_package or not args.add_packages_to_manifest:
                raise Exception("No packages were found in the packages path and no additional packages have been "
                                "specified in the command line as dependencies. This run has failed.")
            global best_view_registry
            best_view_registry = get_registry_from_manifest()
            print "View registry from manifest is {0}".format(best_view_registry)
            dependencies = {}
            for p in args.add_packages_to_manifest:
                dependencies[p] = get_version_from_manifest(p)
                dependencies.update(get_filtered_dependencies_from_view_registry(p, dependencies[p]))
            if args.add_package_as_dependency_to_package:
                for p in args.add_package_as_dependency_to_package:
                    p_split = p.split(":")
                    if p_split[0] not in dependencies:
                        raise Exception(
                            "{0} was expected but is missing from the dependencies parsed from the registry".format(
                                p_split[0]))
                    if p_split[1] not in dependencies:
                        raise Exception(
                            "{0} was expected but is missing from the dependencies parsed from the registry".format(
                                p_split[1]))

            print "Checking if the correct packages exist in the target registry."
            for name in dependencies.keys():
                version = dependencies[name]
                registry_version = _get_version_in_registry(name, args.publish_registry)
                if registry_version == version:
                    dependencies.pop(name)
                    continue
                print "  {0}@{1} is missing in the target registry (the latest one available is '{2}'. Will publish it.".format(
                    name, version, registry_version)

            if len(dependencies) == 0:
                print "It looks like all packages already exist on the publish_registry. So we don't need to republish anything"
            else:
                print "The following packages are missing from the publish registry. Will republish them now"
                for key, value in dependencies.iteritems():
                    tar_path = download_package_tarball(key, value)
                    print "Publishing {0}".format(tar_path)
                    npm_cmd("publish {0}".format(tar_path), args.publish_registry)

            _modify_manifest_registry()
            for key, value in dependencies.iteritems():
                _modify_manifest_for_package(key, value)

        else:
            raise Exception("No package folders found, and --only-publish-existing-packages has not been set. This "
                            "run has failed.")

        git_cmd("add {0}".format(args.packages_path))
        scatter_manifest()

        should_push = create_commit()

        if should_push:
            if args.dry_run:
                print "Would have pushed to remote target/{0} now if --dry-run wasn't set. So skipping this." \
                    .format(args.target_branch)
            else:
                print "Pushing squashed branch publishStable-temp to remote target/{0}".format(args.target_branch)
                git_cmd("push target publishStable-temp:{0}".format(args.target_branch))
        else:
            print "Marking as failed, since we didn't push a commit"
            sys.exit(-1)
    except subprocess.CalledProcessError as e:
        print e
        print e.output
        raise

    finally:
        print "Running cleanup"
        os.chdir(repo_dir)

        if os.path.isdir("node_modules"):
            shutil.rmtree("node_modules")

        if os.path.isdir("etc"):
            shutil.rmtree("etc")

        git_cmd("checkout {0}".format(source_branch))

        os.chdir(root_dir)

def parseArgumentList(argList): # pragma: no cover
    parser = argparse.ArgumentParser(description="A tool which finds all internal packages in a Unity project, "
                                                 "publishes them and updates the repo to use them from the upm repo "
                                                 "it gets uploaded to instead and then pushes that to some other repo "
                                                 "with a flat history")
    parser.add_argument('--view-registries', action='append', help="upm registries that the tool will look for "
                                                                   "existing versions of the packages it will publish. "
                                                                   "This is so it will properly increment "
                                                                   "the version number")
    parser.add_argument('--publish-registry', help="The upm registry that should be used for publishing the packages "
                                                   "to when done, this requires that a valid access token exists in "
                                                   "the .npmrc for the registry.", required=True)
    parser.add_argument('--source-repo', default='.', help="The path to the local repo the script should get the "
                                                           "necessary data from and will do temporary modifications "
                                                           "on. Defaults to current working directory if not set")
    parser.add_argument('--target-repo', required=True, help="Path to local or remote repo that will be the target of "
                                                             "the flattened history push at the end. This repo will "
                                                             "get added as a new remote to the repo called 'target'")
    parser.add_argument('--target-branch', required=True, help="The name of the branch to be read from and pushed to "
                                                               "on the target repo. In essence all new changes from "
                                                               "the source revision since last push to the target "
                                                               "branch will get added as a new commit and pushed in "
                                                               "the end.")
    parser.add_argument('--packages-path', required=True, help="Path to where the packages that the tool should "
                                                               "publish exists. It should be a folder where there "
                                                               "exists a manifest.json")
    parser.add_argument('--add-packages-to-manifest', help="If any of the packages aren't currently in the manifest "
                                                           "but should be added as part of this process, then supply "
                                                           "their names here and they will be added as well as the "
                                                           "version", action='append')
    parser.add_argument('--add-package-as-dependency-to-package', help="If you want to specify that one of the "
                                                                       "packages found should be registered as a "
                                                                       "dependency to another one of the local "
                                                                       "packages then specify this here in the format "
                                                                       "package_with_dependency:dependency. For "
                                                                       "example com.unity.entities:com.unity.jobs",
                        action='append')
    parser.add_argument('--dry-run', help="If set the tool will do everything except publishing the new packages and "
                                          "pushing the new commit to the target repo", action='store_true')
    parser.add_argument('--strip-from-commit', action='append', help="Files or folders that should be removed from "
                                                                     "the squashed commit. for example "
                                                                     "--strip-from-commit publishStable.*")
    parser.add_argument('--only-publish-existing-packages',
                        action='store_true', help="If we just want to remodify the manifest files and push along one "
                                                  "squashed repo to another then we probably don't want to allow "
                                                  "creating new packages. So if this flag is set it will abort the "
                                                  "run if it tries to push new packages, instead of just repushing "
                                                  "the same ones")
    parser.add_argument('--whitelist-hidden-paths', action='append', help="The script aborts if it finds paths "
                                                                          "starting with a . in the repo. If you want"
                                                                          " these in you need to whitelist these "
                                                                          "files or folders")
    return parser.parse_args(argList)

if __name__ == "__main__":      # pragma: no cover
    args = parseArgumentList(args)
    main()
