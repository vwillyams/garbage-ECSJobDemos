import argparse
import os
import sys
import glob
import json

file_blacklist = ['.DS_Store']
OSes_available = ['macOS', 'windows', 'linux']
artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"
args = None

def get_url_json(url):
    print "  Getting json from {0}".format(url)
    import urllib2
    response = urllib2.urlopen(url)
    return json.loads(response.read())

def artifactory_search(type):
    # If the build_version contains a slash we treat it as a branch instead of as a version and will use a custom
    # branch build instead

    artifactory_result = {type:{}}

    if '/' not in args.build_version and not args.build_version.startswith("trunk"):
        for os in OSes_available:
            artifact_uri = get_url_json("{0}/{1}".format(artifactory_url,
                                                        "api/search/prop?build={1}&os={2}&type={3}&custom=False&repos={0}".format(
                                                        artifactory_repository, args.build_version, os, type)))['results']
            if artifact_uri:
                artifactory_result[type][os]=artifact_uri[0]['uri']
    else:
        for os in OSes_available:
            artifact_uri = get_url_json("{0}/{1}".format(artifactory_url,
                                                         "api/search/prop?build=&os={0}&type={1}&custom=True&branch={2}&repos={3}".format(
                                                         os, type, args.build_version, artifactory_repository)))['results']

            if artifact_uri:
                artifactory_result[type][os] = artifact_uri[0]['uri']

    return artifactory_result

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

def generate_unity_revisions_file(editor, mono, il2cpp):
    json_data = {}
    for key in editor:
        json_data[key] = editor[key]
    for key in mono:
        json_data[key] = mono[key]
    for key in il2cpp:
        json_data[key] = il2cpp[key]

    with open('unity_revision.txt', 'wb') as f:
        json.dump(json_data, f, indent = 4)

def main():
    editor_artifacts = artifactory_search("editor")
    standalone_artifacts_mono = artifactory_search("standalone-mono")
    standalone_artifacts_il2cpp = artifactory_search("standalone-il2cpp")

    generate_unity_revisions_file(editor_artifacts, standalone_artifacts_mono, standalone_artifacts_il2cpp)

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
