import argparse
import os
import glob
import utils

artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"
args = None


def artifactory_search_for_revision(type):
    # If the build_version contains a slash we treat it as a branch instead of as a version and will use a custom
    # branch build instead
    artifactory_result = {type: {}}

    # First we get all the artifacts that match our query
    if '/' not in args.build_version and not args.build_version.startswith("trunk"):
        artifact_uri = utils.get_url_json("{0}/{1}".format(artifactory_url,
                                                     "api/search/prop?build={1}&type={2}&custom=False&repos={0}".format(
                                                         artifactory_repository, args.build_version, type)))['results']
    else:
        artifact_uri = utils.get_url_json("{0}/{1}".format(artifactory_url,
                                                     "api/search/prop?build=&type={0}&custom=True&branch={1}&repos={2}".format(
                                                         type, args.build_version, artifactory_repository)))['results']

    if len(artifact_uri) == 0:
        raise Exception("Found no artifactory artifacts matching {0}.".format(args.build_version))
    revision = None
    # Here we do a new api request which requests the revision property. Sadly this has to be two separte api calls
    for artifact in artifact_uri:
        # /api/storage/libs-release-local/org/acme?properties\[=x[,y]\]
        revision_result = utils.get_url_json("{0}?properties=revision".format(artifact['uri']))
        if revision is None:
            revision = revision_result['properties']['revision'][0]
            continue
        # Just some sanity checking so we don't get mismatching revisions here. There should only be one revision
        if revision == revision_result['properties']['revision'][0]:
            continue
        raise Exception(
            "Found multiple revisions on artifactory that matches {0}. The revisions are {1} and {2}".format(
                args.build_version, revision, revision_result['properties']['revision'][0]))

    if not revision:
        raise Exception("Could not find any revision for {0}".format(args.build_version))

    return revision


def get_packages_folder(project_path):
    if os.path.isfile(os.path.join(project_path, "Packages/manifest.json")):
        return os.path.abspath(os.path.join(project_path, "Packages"))
    raise Exception("Unable to find manifest for project {0}".format(project_path))


def get_packages(packages_folder):
    packages = []
    for f in glob.glob(os.path.join(packages_folder, "**/package.json")):
        packages.append(os.path.dirname(f))
    return packages


def list_files_and_directories_in_folder(folder):
    return os.listdir(folder)


# see https://docs.unity3d.com/Manual/SpecialFolders.html for including rules in unity
def should_ignore(path):
    if path.startswith('.') or path.endswith('~') or path == 'cvs' or path.endswith('.tmp'):
        return True

    return False


def check_metafiles_in_package(package_folder):
    root_files = list_files_and_directories_in_folder(package_folder)
    for file in root_files:
        if should_ignore(file):
            continue

        if os.path.isdir(file):
            check_metafiles_in_package(file)
        if not file.endswith("meta") and not file + ".meta" in root_files:
            raise Exception(
                'Missing {0}.meta in package {1}. Packages require every file and directory to have a meta file associated.'.format(
                    file, package_folder))
    return True


def generate_unity_revisions_file(revision):
    with open('unity_revision.txt', 'wb') as f:
        f.write(revision)


def main():
    revision = artifactory_search_for_revision("editor")

    generate_unity_revisions_file(revision)

    for package in args.package_path:
        if not check_metafiles_in_package(package):
            raise Exception


def parse_argument_list():
    parser = argparse.ArgumentParser(description="A tool which performs sanity checks against the packages and demo "
                                                 "that are going to be tested published in the next CI steps.")

    parser.add_argument('--package-path', action='append', required=True, help="Path to where the package exists. "
                                                                               "It should be a folder where there "
                                                                               "exists a package.json")

    parser.add_argument('--build-version', required=True, help="Unity build version. If it contains a '/' we treat "
                                                               "it as a branch instead of as a version and will use"
                                                               " a custom branch instead")

    return parser.parse_args()


if __name__ == "__main__":
    args = parse_argument_list()
    main()
