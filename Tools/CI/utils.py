import json
import os
import shutil
import tarfile
import zipfile

import requests


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


def extract_tarball(download_path, extract_path):
    print "  Extracting %s into %s" % (download_path, extract_path)
    tar = tarfile.open(download_path, "r:gz")
    tar.extractall(extract_path)
    tar.close()


def download_url(url, filename):
    print "  Downloading %s to %s" % (url, filename)

    r = requests.get(url, stream=True)
    with open(filename, 'wb') as f:
        shutil.copyfileobj(r.raw, f)


def extract_zip(archive, destination):
    print "  Extracting %s into %s" % (archive, destination)
    import zipfile
    if get_current_os() == "windows":
        zip_ref = ZipfileLongWindowsPaths(archive, 'r')
    else:
        zip_ref = zipfile.ZipFile(archive, 'r')
    zip_ref.extractall(destination)
    zip_ref.close()


def winapi_path(dos_path, encoding=None):
    path = os.path.abspath(dos_path)

    if path.startswith("\\\\"):
        path = "\\\\?\\UNC\\" + path[2:]
    else:
        path = "\\\\?\\" + path

    return path


class ZipfileLongWindowsPaths(zipfile.ZipFile):

    def _extract_member(self, member, targetpath, pwd):
        targetpath = winapi_path(targetpath)
        return zipfile.ZipFile._extract_member(self, member, targetpath, pwd)
