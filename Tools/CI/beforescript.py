import json
import os
import tarfile

import sys

artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"
build_version = None


def get_current_os():
    import sys
    p = sys.platform
    if p == "darwin":
        return "macOS"
    if p == "win32":
        return "windows"
    return "linux"


def get_url_json(url):
    print "  Getting json from {0}".format(url)
    import urllib2
    response = urllib2.urlopen(url)
    return json.loads(response.read())


def download_url(url, filename):
    import urllib
    urllib.urlretrieve(url, filename)


def artifactory_search(type):
    # If the build_version contains a slash we treat it as a branch instead of as a version and will use a custom
    # branch build instead

    if '/' not in build_version and not build_version.startswith("trunk"):
        return get_url_json("{0}/{1}".format(artifactory_url, "api/search/prop?build={1}&os={2}&type={3}&custom=False&repos={0}".format(artifactory_repository, build_version, get_current_os(), type)))

    return get_url_json("{0}/{1}".format(artifactory_url,
                                         "api/search/prop?build=&os={0}&type={1}&custom=True&branch={2}&repos={3}".format(
                                             get_current_os(), type, build_version, artifactory_repository, )))


def extract_tarball(download_path, extract_path):
    tar = tarfile.open(download_path, "r:gz")
    tar.extractall(extract_path)
    tar.close()


def download_artifact(url, extract_path):
    if not os.path.exists(extract_path):
        os.makedirs(extract_path)
    data = get_url_json(url)
    print "Downloading {0} and extracting it in {1}".format(url, extract_path)
    download_path = os.path.join("temp", data['downloadUri'].split('/')[-1])
    download_url(data['downloadUri'], download_path)
    print "Extracting {0} to {1}".format(download_path, extract_path)
    if data['downloadUri'].endswith(".zip"):
        extract_zip(download_path, extract_path)
    elif data['downloadUri'].endswith(".tar.gz"):
        extract_tarball(download_path, extract_path)
    else:
        raise Exception("Wanted to extract file with unknown file ending: {0}".format(data['downloadUri']))


def extract_zip(archive, destination):
    import zipfile
    zip_ref = zipfile.ZipFile(archive, 'r')
    zip_ref.extractall(destination)
    zip_ref.close()


def main():
    if not os.path.exists("temp"):
        os.mkdir("temp")

    editor_artifacts = artifactory_search("editor")['results']
    standalone_artifacts = [artifactory_search("standalone-mono")['results'],
                            artifactory_search("standalone-il2cpp")['results']]

    if len(editor_artifacts) != 1:
        raise Exception("Expected to find exactly 1 editor build to use. Found: {0}".format(editor_artifacts))
    if len(standalone_artifacts) != 2:
        raise Exception("Expected to find exactly 2 standalonesupport build to use. Found: {0}".format(standalone_artifacts))

    download_artifact(editor_artifacts[0]['uri'], ".Editor")

    current_os = get_current_os()

    for standalone in standalone_artifacts:
        if len(standalone) != 1:
            raise Exception(
                "Expected to find exactly 1 standalonesupport build to use. Found: {0}".format(standalone))

        if current_os == "windows":
            download_artifact(standalone[0]['uri'], ".Editor/Data/PlaybackEngines/windowsstandalonesupport")
        elif current_os == "macOS":
            download_artifact(standalone[0]['uri'], ".Editor/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport")


if __name__ == "__main__":
    build_version = sys.argv[1]
    main()
