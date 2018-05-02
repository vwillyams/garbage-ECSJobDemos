import os
import sys

import utils

artifactory_url = "https://artifactory.eu-cph-1.unityops.net/"
artifactory_repository = "core-automation"


def download_unity_launcher(type):
    if not os.path.exists(".tmp"):
        os.mkdir(".tmp")
    utils.download_url("https://artifactory.eu-cph-1.unityops.net/core-automation/tools/unity-launcher/UnityLauncher.%s.zip" % type,
                       ".tmp/UnityLauncher.%s.zip" % type)


def extract_unity_launcher(type):
    utils.extract_zip(".tmp/UnityLauncher.%s.zip" % type,
                      ".UnityLauncher.%s" % type)
    current_os = utils.get_current_os()
    if current_os == "macOS":
        os.chmod('.UnityLauncher.{0}/osx.10.12-x64/publish/UnityLauncher.{0}'.format(type), 0755)
    elif current_os == "linux":
        os.chmod('.UnityLauncher.{0}/linux-x64/publish/UnityLauncher.{0}'.format(type), 0755)


if __name__ == "__main__":
    type = sys.argv[1]


def prepare_unity_launcher(run_type):

    download_unity_launcher(run_type)
    extract_unity_launcher(run_type)


def artifactory_search(type, revision):
    result = utils.get_url_json("{0}/{1}".format(artifactory_url,
                                         "api/search/prop?&os={0}&type={1}&revision={2}&repos={3}".format(
                                             utils.get_current_os(), type, revision, artifactory_repository)))['results']
    if len(result) != 1:
        raise Exception("Expected to find exactly 1 {0} for revision {1} build to use. Found: {2}".format(type, revision, len(result)))
    return result


def download_artifact(url, extract_path):
    if not os.path.exists(extract_path):
        os.makedirs(extract_path)
    data = utils.get_url_json(url)
    print "Downloading {0} and extracting it in {1}".format(url, extract_path)
    download_path = os.path.join("temp", data['downloadUri'].split('/')[-1])
    utils.download_url(data['downloadUri'], download_path)
    print "Extracting {0} to {1}".format(download_path, extract_path)
    if data['downloadUri'].endswith(".zip"):
        utils.extract_zip(download_path, extract_path)
    elif data['downloadUri'].endswith(".tar.gz"):
        utils.extract_tarball(download_path, extract_path)
    else:
        raise Exception("Wanted to extract file with unknown file ending: {0}".format(data['downloadUri']))


def get_unity_revision():
    f = open("unity_revision.txt", 'r')
    revision = f.read().strip()
    print "Unity revision to use is: {0}".format(revision)
    if not revision:
        raise Exception("Could not find a valid revision in unity_revision.txt")
    return revision


def main():
    run_type = sys.argv[1]
    prepare_unity_launcher(run_type)
    if run_type != "Editor":
        return

    revision = get_unity_revision()
    if not os.path.exists("temp"):
        os.mkdir("temp")

    editor_artifacts = artifactory_search("editor", revision)
    standalone_artifacts = [artifactory_search("standalone-mono", revision),
                            artifactory_search("standalone-il2cpp", revision)]

    download_artifact(editor_artifacts[0]['uri'], ".Editor")

    current_os = utils.get_current_os()

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
