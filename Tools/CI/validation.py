import argparse
import os
import sys
import glob
import json

file_blacklist = ['.DS_Store']
artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"
args = None

def get_url_json(url):
    print "  Getting json from {0}".format(url)
    import urllib2
    response = urllib2.urlopen(url)
    return json.loads(response.read())

def artifactory_search_for_revision(type):
    # If the build_version contains a slash we treat it as a branch instead of as a version and will use a custom
    # branch build instead
    artifactory_result = {type:{}}

    # First we get all the artifacts that match our query
    if '/' not in args.build_version and not args.build_version.startswith("trunk"):
        artifact_uri = get_url_json("{0}/{1}".format(artifactory_url,
                                                    "api/search/prop?build={1}&type={2}&custom=False&repos={0}".format(
                                                    artifactory_repository, args.build_version, type)))['results']
    else:
        artifact_uri = get_url_json("{0}/{1}".format(artifactory_url,
                                                     "api/search/prop?build=&type={0}&custom=True&branch={1}&repos={2}".format(type, args.build_version, artifactory_repository)))['results']

    if len(artifact_uri) == 0:
        raise Exception("Found no artifactory artifacts matching {0}.".format(args.build_version))
    revision = None
    # Here we do a new api request which requests the revision property. Sadly this has to be two separte api calls
    for artifact in artifact_uri:
        # /api/storage/libs-release-local/org/acme?properties\[=x[,y]\]
        revision_result = get_url_json("{0}?properties=revision".format(artifact['uri']))
        if revision is None:
            revision = revision_result['properties']['revision'][0]
            continue
        # Just some sanity checking so we don't get mismatching revisions here. There should only be one revision
        if revision == revision_result['properties']['revision'][0]:
            continue
        raise Exception("Found multiple revisions on artifactory that matches {0}. The revisions are {1} and {2}".format(args.build_version, revision, revision_result['properties']['revision'][0]))

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

def check_metafiles_in_package(package_folder):
    files = list_files_and_directories_in_folder(package_folder)
    for file in files:
        if not file in file_blacklist and not file.endswith("meta") and not file + ".meta" in files:
            print 'Missing {0}.meta in package {1}. Packages require every file and directory to have a meta file associated.'.format(file, package_folder)
            return False
    return True

def generate_unity_revisions_file(revision):
    with open('unity_revision.txt', 'wb') as f:
        f.write(revision)

def main():
    revision = artifactory_search_for_revision("editor")

    generate_unity_revisions_file(revision)

    for project_path in args.project_path:
        packages_folder = get_packages_folder(project_path[0])
        packages = get_packages(packages_folder)
        for package in packages:
            if not check_metafiles_in_package(package):
                raise Exception
            #check_code_style(package)

        # check_code_style(project)

def parse_argument_list():
    parser = argparse.ArgumentParser(description="A tool which performs sanity checks against the packages and demo "
                                                 "that are going to be tested published in the next CI steps.")

    parser.add_argument('--project-path', nargs='*', action='append', required=True, help="Path to where the projects exist. It should be a folder "
                                                              "where there exists a Package/manifest.json")

    parser.add_argument('--build-version', required=True, help="Unity build version. If it contains a '/' we treat "
                                                                    "it as a branch instead of as a version and will use"
                                                                    " a custom branch instead")

    return parser.parse_args()


if __name__ == "__main__":
    args = parse_argument_list()
    main()
