import argparse
import os
import sys
import glob

file_blacklist = [ '.DS_Store']

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

def main():
    for project_path in args.project_path:
        packages_folder = get_packages_folder(project_path)
        packages = get_packages(packages_folder)
        for package in packages:
            if not check_metafiles_in_package(package):
                raise Exception
            #check_code_style(package)

        # check_code_style(project)

def parse_argument_list():
    parser = argparse.ArgumentParser(description="A tool which performs sanity checks against the packages and demo "
                                                 "that are going to be tested published in the next CI steps.")

    parser.add_argument('--project-path', required=True, help="Path to where the packages that the tool should "
                                                               "publish exists. It should be a folder where there "
                                                               "exists a manifest.json")

    return parser.parse_args()

if __name__ == "__main__":

    args = parse_argument_list()
    main()
