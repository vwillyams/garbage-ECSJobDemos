import filecmp
import glob
import os
import subprocess
import shutil

dry_run = True
dest_repo = "D:/playground/ghost"
dest_branch = "master"
view_registries = ["https://packages.unity.com", "https://staging-packages.unity.com", "http://10.45.32.202:4873"]
best_view_registry = None
publish_registry = "http://10.45.32.202:4873"


def get_packages_folder():
    # type: () -> str
    path = "ECSJobDemos/Packages/"
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
    npm_cmd("install --no-package-lock --prefix . {0}".format(package_name), best_view_registry, False)
    installed_path = os.path.abspath(os.path.join("node_modules", package_name))
    print "Comparing files between {0} and {1}".format(installed_path, package_folder)

    package_files = _get_all_files(installed_path)
    repo_files = _get_all_files(os.path.abspath(package_folder))

    mismatching_files = list(set(package_files).symmetric_difference(set(repo_files)))

    if len(mismatching_files) > 0:
        print "  The number of files don't match. {0} mismatching files".format(len(mismatching_files))
        return True

    #package_files = [os.path.join(installed_path, s) for s in package_files]
    #repo_files = [os.path.join(package_folder, s) for s in repo_files]

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
    for registry in view_registries:
        try:
            version = npm_cmd("view {0} version".format(package_name), registry, False)

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

    print "{0} was selected as the best registry to ready from since it had the highest package version for {1} ({2})".format(
        best_view_registry, package_name, highest_version)

    return highest_version


def publish_new_package(package_path, version):
    # type: () -> str
    previous_cwd = os.getcwd()
    try:

        os.chdir(package_path)
        npm_cmd("--no-git-tag-version version {0} --allow-same-version".format(version), None, False).strip()
        print "Packing as version {0}".format(version)
        package_archive = npm_cmd("pack .", None, False).strip()
        print "Publishing {0} to {1}".format(package_archive, publish_registry)
        npm_cmd("publish {0}".format(package_archive), publish_registry, True)
        os.remove(package_archive)
    finally:
        os.chdir(previous_cwd)
    pass


def modify_manifest(name, version):
    pass


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
        git_cmd("rm {0}".format(d), False)

    git_cmd("checkout master -- .", False)

    git_cmd("add -A")
    git_cmd("commit -m \"Current squash\" --allow-empty", False)
    pass


def create_commit():
    # type: () -> bool

    print "Committing changes since merge"
    exists = git_cmd_code_only("fetch target {0}".format(dest_branch), False)

    if exists != 0:
        git_cmd("commit -m \"Release 1\" --amend", False)
        return True

    previous_release = git_cmd("log target/{0} -1 --pretty=%B".format(dest_branch), False).split(" ")
    next_release = "Release {0}".format(int(previous_release[1]) + 1)

    git_cmd("commit -m \"{0}\" --amend --allow-empty".format(next_release), False)

    # Checking if the last two commits in this branch has any changes between them. If no then we shouldn't push
    anything_changed = git_cmd("diff @~..@", False)
    if not anything_changed.strip():
        print "Nothing has changed at all. Won't push commit"
        return False
    return True


def remove_this_script_from_commit():
    pass


def git_cmd(cmd, is_destructive=True):
    formatted_cmd = "git {0}".format(cmd)
    if is_destructive and dry_run:
        print "  Skipping: {0}".format(formatted_cmd)
        return None
    print "  Running: {0}".format(formatted_cmd)
    return subprocess.check_output(formatted_cmd, shell=True, stderr=subprocess.STDOUT)


def git_cmd_code_only(cmd, is_destructive=True):
    formatted_cmd = "git {0}".format(cmd)
    if is_destructive and dry_run:
        print "  Skipping: {0}".format(formatted_cmd)
        return None
    print "  Running: {0}".format(formatted_cmd)
    with open(os.devnull, 'w') as devnull:
        return subprocess.call(formatted_cmd, shell=True, stderr=devnull, stdout=devnull)


def npm_cmd(cmd, registry, is_destructive=True):
    registry_cmd = ''
    if registry:
        registry_cmd = "--registry {0}".format(registry)

    formatted_cmd = 'npm {0} {1}'.format(cmd, registry_cmd)
    if is_destructive and dry_run:
        print "  Skipping: {0}".format(formatted_cmd)
        return None

    print "  Running: {0}".format(formatted_cmd)
    return subprocess.check_output(formatted_cmd, shell=True, stderr=subprocess.STDOUT)


def add_destination_repo():
    print "Adding $dstRepo as remote to repo"
    remotes = git_cmd("remote", False)
    if "target" not in remotes:
        git_cmd("remote add target {0}".format(dest_repo), False)
    git_cmd("fetch target", False)
    branches = git_cmd("ls-remote --heads target", False)
    local_branches = git_cmd("branch", False)

    # Lets just always remove it so we stay clean
    if "publishing" in local_branches:
        git_cmd("checkout origin/master", False)
        git_cmd("branch -D publishing", False)

    if "refs/heads/{0}".format(dest_branch) in branches:
        git_cmd("checkout -b publishing --track target/{0}".format(dest_branch), False)
    else:
        git_cmd("checkout --orphan publishing", False)


def main():
    root_dir = os.getcwd()
    try:
        print "Will now start creating packages for publish from $srcRepo to $dstRepo"
        root_clone = os.getcwd()
        add_destination_repo()
        # print "Removing $srcRepo from remote of repo"
        # git_cmd("remote remove origin", False)
        squash_commits()
        packages_folder = get_packages_folder()
        packages = get_list_of_packages(packages_folder)

        for package_path in packages:
            package_name = os.path.basename(os.path.normpath(package_path))
            print "### Package Found: {0} in {1}".format(package_name, package_path).ljust(80, '#')
            current_package_version = get_package_version(package_name)
            changed = is_package_changed(package_path, package_name)
            if changed is True:
                print "The package repository is not clean. A new version of $package will be published"
                new_package_version = increase_version(current_package_version, False, False, True)
                publish_new_package(package_path, new_package_version)
                modify_manifest(package_path, new_package_version)
            else:
                print "No change detected in the repo. The version current will be used in the project"

            os.chdir(packages_folder)
            if not dry_run:
                shutil.rmtree(package_path)

            os.chdir(root_clone)
            git_cmd("add {0}".format(package_path), False)
            print ''.ljust(80, '#')

        shutil.rmtree("node_modules")
        scatter_manifest()

        remove_this_script_from_commit()

        should_push = create_commit()
        if should_push:
            print "Pushing squashed branch publishing to remote target/{0}".format(dest_branch)
            git_cmd("push target publishing:{0}".format(dest_branch), False)
        os.chdir(root_dir)
    except subprocess.CalledProcessError as e:
        print e
        print e.output
        raise
    finally:
        os.chdir(root_dir)
        # TODO restore has been modified during a dry run
        if dry_run:
            print "Doing git reset & clean"
            # git_cmd("reset --hard HEAD", False)
            # git_cmd("clean -f -d", False)

        if os.path.isdir("etc"):
            shutil.rmtree("etc")

        pass


if __name__ == "__main__":
    main()
