import os

import shutil

if __name__ == "__main__":
    if os.path.exists("temp"):
        print "Deleting temp folder"
        shutil.rmtree("temp")
    if os.path.exists(".Editor"):
        print "Deleting .Editor folder"
        shutil.rmtree(".Editor")
