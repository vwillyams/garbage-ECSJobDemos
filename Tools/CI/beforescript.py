import json
import os
import tarfile

artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"
build_version = "2018.1.0b13"


def get_current_os():
    import sys
    p = sys.platform
    if p == "darwin":
        return "macOS"
    if p == "win32":
        return "windows"
    return "linux"


def get_url_json(url):
    import urllib2
    response = urllib2.urlopen(url)
    return json.loads(response.read())


def download_url(url, filename):
    import urllib
    urllib.urlretrieve(url, filename)


def artifactory_search(type):
    return get_url_json("{0}/{1}".format(artifactory_url, "api/search/prop?build={1}&os={2}&type={3}&repos={0}".format(artifactory_repository, build_version, get_current_os(), type)))


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
    standalone_artifacts = artifactory_search("standalonesupport")['results']

    if len(editor_artifacts) != 1:
        raise Exception("Expected to find exactly 1 editor build to use. Found: {0}".format(editor_artifacts))
    if len(standalone_artifacts) != 1:
        raise Exception("Expected to find exactly 1 standalonesupport build to use. Found: {0}".format(standalone_artifacts))

    download_artifact(editor_artifacts[0]['uri'], "Editor")

    current_os = get_current_os()

    if current_os == "windows":
        download_artifact(standalone_artifacts[0]['uri'], "Editor/Data/PlaybackEngines/windowsstandalonesupport")
    elif current_os == "macOS":
        download_artifact(standalone_artifacts[0]['uri'], "Editor/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport")


if __name__ == "__main__":
    main()
