import filecmp
import glob
import os
import subprocess
import shutil
import sys
import argparse
import json

args = argparse.Namespace()
source_branch = ""
local_packages = {}
modified_packages = {}


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


def _get_all_files(path):
    return [os.path.relpath(os.path.join(dp, f), path) for dp, dn, fn in os.walk(os.path.expanduser(path)) for f in fn]


def is_package_changed(package_folder, package_name):
    # type: (str) -> bool
    print "Installing {0} from {1} to see if we have changed anything".format(package_name, best_view_registry)
    npm_cmd("install --no-package-lock --prefix . {0}".format(package_name), best_view_registry)
    installed_path = os.path.abspath(os.path.join("node_modules", package_name))
    print "Comparing files between {0} and {1}".format(installed_path, package_folder)

    package_files = _get_all_files(installed_path)
    repo_files = _get_all_files(os.path.abspath(package_folder))

    mismatching_files = list(set(package_files).symmetric_difference(set(repo_files)))

    if len(mismatching_files) > 0:
        print "  The number of files don't match. {0} mismatching files".format(len(mismatching_files))
        return True

    match, mismatch, errors = filecmp.cmpfiles(package_folder, installed_path, repo_files)

    if len(mismatch) == 0 and len(errors) == 0:
        print "Nothing has changed"
        return False
    print "  The following files have changed compared to the currently published package:"
    for m in mismatch:
        print "    {0}".format(m)
    return True
    pass


def increase_version(version, major, minor, patch):
    trimmed_version = version.strip().split('+')[0].split('.')
    new_major = int(trimmed_version[0])
    new_minor = int(trimmed_version[1])
    new_patch = int(trimmed_version[2])
    if major:
        new_major += 1
    if minor:
        new_minor += 1
    if patch:
        new_patch += 1
    new_version = "{0}.{1}.{2}".format(new_major, new_minor, new_patch)
    print "New version is changed from {0} to {1}".format(version, new_version)
    return new_version


def get_package_version(package_name, ):
    # type: (str) -> str

    global best_view_registry
    print "Getting current published package version for {0}".format(package_name)
    highest_version = "0.0.0"

    highest_version_trimmed = highest_version.split('.')
    for registry in args.view_registries:
        try:
            version = npm_cmd("view {0} version".format(package_name), registry)

        # Hack for sinopia which returns 404 when a package doesn't exist, whereas no other registry does
        except subprocess.CalledProcessError as e:
            if "is not in the npm registry" in e.output:
                version = ""
            else:
                raise e
        if not version:
            # Package didn't exist in the registry
            continue
        trimmed_version = version.strip().split('+')[0].split('.')
        for i in range(0, 3):
            if int(trimmed_version[i]) > int(highest_version_trimmed[i]):
                highest_version = version.strip()
                highest_version_trimmed = trimmed_version
                best_view_registry = registry

    print "{0} was selected as the best registry to ready from since it had the highest package version for {1} ({2})" \
        .format(best_view_registry, package_name, highest_version)

    return highest_version


def publish_new_package(package_name, version):
    # type: (str, str) -> None
    previous_cwd = os.getcwd()
    try:

        os.chdir("{0}/{1}".format(args.packages_path, package_name))
        # TODO: Remove --allow-same-version
        npm_cmd("--no-git-tag-version version {0} --allow-same-version".format(version), None).strip()
        print "Packing as version {0}".format(version)
        package_archive = npm_cmd("pack .", None).strip()
        print "Publishing {0} to {1}".format(package_archive, args.publish_registry)
        npm_cmd("publish {0}".format(package_archive), args.publish_registry)
        os.remove(package_archive)
    finally:
        os.chdir(previous_cwd)
    pass


def _modify_manifest(package_name, version):
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


def _modify_package_file_dependencies(package_name, version):
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
    manual_dependencies = [dep.replace("{0}:".format(package_name), "") for dep in args.add_package_as_dependency_to_package if dep.startswith("{0}:".format(package_name))]

    for modified_package_name, modified_version in local_packages.iteritems():
        if modified_package_name in dependencies or modified_package_name in manual_dependencies:
            package_file["dependencies"][modified_package_name] = modified_version
            modified = True

    if modified:
        with open("{0}/{1}/package.json".format(args.packages_path, package_name), 'w') as outfile:
            json.dump(package_file, outfile, indent=4)


def modify_json_dependencies(package_name, version):
    _modify_manifest(package_name, version)
    _modify_package_file_dependencies(package_name, version)


def scatter_manifest():
    pass


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

    previous_release = git_cmd("log target/{0} -1 --pretty=%B".format(args.target_branch)).split(" ")
    next_release = "Release {0}".format(int(previous_release[1]) + 1)

    git_cmd("commit -m \"{0}\" --amend --allow-empty".format(next_release))

    # Checking if the last two commits in this branch has any changes between them. If no then we shouldn't push
    anything_changed = git_cmd("diff @~..@")
    if not anything_changed.strip():
        print "Nothing has changed at all. Won't push commit"
        return False
    return True


def remove_this_script_from_commit():
    pass


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
    remotes = git_cmd("remote")
    if "target" not in remotes:
        git_cmd("remote add target {0}".format(args.target_repo))
    git_cmd("fetch target")
    branches = git_cmd("ls-remote --heads target")
    local_branches = git_cmd("branch")

    # Lets just always remove it so we stay clean
    if "publishing" in local_branches:
        git_cmd("checkout {0}".format(source_branch))
        git_cmd("branch -D publishing")

    if "refs/heads/{0}".format(args.target_branch) in branches:
        git_cmd("checkout -b publishing --track target/{0}".format(args.target_branch))
    else:
        git_cmd("checkout --orphan publishing")


def process_package(package_path, package_name, root_clone):
    current_package_version = get_package_version(package_name)
    changed = is_package_changed(package_path, package_name)
    if changed is True:
        if args.dry_run:
            print "The package {0} has been changed but --dry-run has been set so it will not get published" \
                .format(package_name)
        else:
            print "The package has been modified since latest published version. A new version of {0} will be " \
                  "published".format(package_name)
        new_package_version = increase_version(current_package_version, False, False, True)
        local_packages[package_name] = new_package_version
        modified_packages[package_name] = new_package_version
    else:
        print "No change detected in the repo. The current version of {0} ({1}) will be used in the project" \
            .format(package_name, current_package_version)
        local_packages[package_name] = current_package_version

    os.chdir(root_clone)
    print ''.ljust(80, '#')


def publish_modified_packages():
    # we update all the manifest and package json files first before we publish so everything has the right version
    for package_name, version in local_packages.iteritems():
        modify_json_dependencies(package_name, version)

    for package_name, version in modified_packages.iteritems():
        if not args.dry_run:
            publish_new_package(package_name, version)
            shutil.rmtree("./{0}/{1}".format(args.packages_path, package_name))


def main():
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
        source_branch = "master"
        print "Will now start creating packages for publish from {0} to {1}:{2}" \
            .format(source_branch, args.target_repo, args.target_branch)
        root_clone = os.getcwd()
        add_destination_repo()

        squash_commits()
        packages_folder = args.packages_path
        packages = get_list_of_packages(packages_folder)
        late_process_packages = []

        for package_path in packages:
            package_name = os.path.basename(os.path.normpath(package_path))
            print "### Package Found: {0} in {1}".format(package_name, package_path).ljust(80, '#')
            if args.add_packages_to_manifest is not None and package_name in args.add_packages_to_manifest:
                print "Skipping for now since it is to be added to the manifest at the end"
                late_process_packages.append(package_path)
                continue
            process_package(package_path, package_name, root_clone)

        for package_path in late_process_packages:
            package_name = os.path.basename(os.path.normpath(package_path))
            print "### Package Found to add to manifest: {0} in {1}".format(package_name, package_path).ljust(80, '#')
            process_package(package_path, package_name, root_clone)

        publish_modified_packages()
        git_cmd("add {0}".format(args.packages_path))

        shutil.rmtree("node_modules")
        scatter_manifest()

        remove_this_script_from_commit()

        should_push = create_commit()

        if should_push:
            if args.dry_run:
                print "Would have pushed to remote target/{0} now if --dry-run wasn't set. So skipping this." \
                    .format(args.target_branch)
            else:
                print "Pushing squashed branch publishing to remote target/{0}".format(args.target_repo)
                git_cmd("push target publishing:{0}".format(args.target_branch))
        os.chdir(root_dir)
    except subprocess.CalledProcessError as e:
        print e
        print e.output
        raise
    finally:
        print "Running cleanup"
        git_cmd("checkout {0}".format(source_branch))
        os.chdir(root_dir)

        if os.path.isdir("node_modules"):
            shutil.rmtree("node_modules")

        if os.path.isdir("etc"):
            shutil.rmtree("etc")




if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="A tool which finds all internal packages in a Unity project, "
                                                 "publishes them and updates the repo to use them from the upm repo "
                                                 "it gets uploaded to instead and then pushes that to some other repo "
                                                 "with a flat history")
    parser.add_argument('--view-registries', action='append', help="upm registries that the tool will look for "
                                                                   "existing versions of the packages it will publish. "
                                                                   "This is so it will properly increment "
                                                                   "the version number", required=True)
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
    args = parser.parse_args()

    #os.chdir(args.source_repo)
    #global local_packages
    #local_packages = {"com.unity.collections": "2.0.0", "com.unity.jobs": "3.0.0"}
    #modify_json_dependencies("com.unity.entities", "1.0.0")

    main()
