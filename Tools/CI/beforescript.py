import json
import os
import tarfile

import sys

artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"


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


def artifactory_search(type, revision):
    result = get_url_json("{0}/{1}".format(artifactory_url,
                                         "api/search/prop?&os={0}&type={1}&revision={2}&repos={3}".format(
                                             get_current_os(), type, revision, artifactory_repository)))['results']
    if len(result) != 1:
        raise Exception("Expected to find exactly 1 {0} for revision {1} build to use. Found: {2}".format(type, revision, len(result)))
    return result


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


def get_unity_revision():
    f = open("unity_revision.txt", 'r')
    revision = f.read().strip()
    print "Unity revision to use is: {0}".format(revision)
    if not revision:
        raise Exception("Could not find a valid revision in unity_revision.txt")
    return revision


def main():
    revision = get_unity_revision()
    if not os.path.exists("temp"):
        os.mkdir("temp")

    editor_artifacts = artifactory_search("editor", revision)
    standalone_artifacts = [artifactory_search("standalone-mono", revision),
                            artifactory_search("standalone-il2cpp", revision)]

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
    main()
