import json
import os
import shutil

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
    # /api/search/gavc?g=org.acme&a=artifact&v=1.0&c=sources&repos=libs-release-local
    return get_url_json("{0}/{1}".format(artifactory_url, "api/search/prop?build={1}&os={2}&type={3}&repos={0}".format(artifactory_repository, build_version, get_current_os(), type)))


def download_artifact(url, extract_path):
    if not os.path.exists(extract_path):
        os.makedirs(extract_path)
    data = get_url_json(url)
    print data
    download_path = os.path.join("temp", data['downloadUri'].split('/')[-1])
    download_url(data['downloadUri'], download_path)
    extract_zip(download_path, extract_path)


def extract_zip(archive, destination):
    import zipfile
    zip_ref = zipfile.ZipFile(archive, 'r')
    zip_ref.extractall(destination)
    zip_ref.close()


def main():
    try:
        if not os.path.exists("temp"):
            os.mkdir("temp")
        for uri in artifactory_search("editor")['results']:
            print uri['uri']
            download_artifact(uri['uri'], "Editor")

        current_os = get_current_os()
        for uri in artifactory_search("standalonesupport")['results']:
            print uri['uri']
            if current_os == "windows":
                download_artifact(uri['uri'], "Editor/Data/PlaybackEngines/windowsstandalonesupport")
            elif current_os == "macOS":
                download_artifact(uri['uri'], "Editor/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport")
    finally:
        shutil.rmtree("temp")


if __name__ == "__main__":
    main()
