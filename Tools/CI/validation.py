import argparse
import os

args = argparse.Namespace()

def get_packages_folder(project_path):
    if os.path.isfile(project_path + "manifest.json"):
        return os.path.abspath(project_path)
    raise Exception("Unable to find {0}".format(project_path))

def get_packages(package_folder):
    packages = []
    for f in glob.glob(os.path.join(folder, "**/package.json")):
        packages.append(os.path.dirname(f))
    return packages

def list_files_and_directories_in_folder(folder):
    pass

def check_metafiles_in_package(packages_folder):
    files = list_files_and_directories_in_folder(packages_folder)
    for file in files:
        if not file.endswith("meta") and not files.contain(file + ".meta"):
            return False
    return True

def main():
    for project_path in args.packages_path:
        packages_folder = get_packages_folder(project_path)
        packages = get_packages(packages_folder)
        for package in packages:
            check_metafiles_in_package(package)
            #check_code_style(package)

        # check_code_style(project)

def parse_argument_list(argList):
    parser = argparse.ArgumentParser(description="A tool which performs sanity checks against the packages and demo "
                                                 "that are going to be tested published in the next CI steps.")

    parser.add_argument('--packages-path', required=True, help="Path to where the packages that the tool should "
                                                               "publish exists. It should be a folder where there "
                                                               "exists a manifest.json")

    return parser.parse_args(argList)

if __name__ == "__main__":
    args = parse_argument_list(args)
    main()
