# TODO:
#  - smarter indent on file writes
#  - handle spaces in paths
import json
from abc import ABCMeta, abstractmethod

import os


def _get_os_argument_delimiter(os):
    os_argument_delimiter = "'"
    if os.lower().startswith("win"):
        os_argument_delimiter = "\""
    return os_argument_delimiter


class IStage(object):
    __metaclass__ = metaclass = ABCMeta

    _name=""

    @abstractmethod
    def generate(self, file, tag, variation, projects, backends, schedule_only, dependencyStage):
        pass

class BuildStage(IStage):
    _name="build"
    def generate(self, file, tag, variation, projects, backends, schedule_only, dependencyStage):
        for backend in backends:
            for project in projects:
                if self._name in project["skip-stage"]:
                    continue
                if 'schedule_only' in project and project['schedule_only'] and not schedule_only:
                    continue
                if 'scenes' in project:
                    for scene in project["scenes"]:
                        self.generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, scene)
                else:
                    self.generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, None)

    @staticmethod
    def generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, scene):
        name_prefix = ""
        if schedule_only:
            name_prefix = "schedule:"

        scene_name = ""
        scene_job_name = ":%s" % project['path']
        if scene:
            scene_name = scene['name']
            scene_job_name = ":%s" % scene['name']

        file.write(name_prefix + tag["name"] + ":build" + scene_job_name + ":" + backend + ":" + _clean_variation(
            variation) + ":\n")
        file.write("  stage: build\n")
        file.write("  before_script:\n")
        file.write("    - python Tools/CI/beforescript.py Editor\n")
        file.write("  after_script:\n")
        file.write("    - python Tools/CI/afterscript.py\n")
        if schedule_only:
            file.write("  only:\n")
        else:
            file.write("  except:\n")
        file.write("    - schedules\n")
        file.write("  tags:\n")
        for t in tag['tags']:
            file.write("  - %s\n" % t)

        file.write("  script:\n")
        file.write("    - " + tag["unity-launcher-editor"])
        file.write(" -unityexecutable \"" + tag["unity-path"] + "\"")
        file.write(" -projectpath " + project["path"])
        file.write(" -batchmode")
        file.write(
            " " + tag["unity-build-player"] + " build/" + backend + "/" + tag["name"] + "-standalone" + tag[
                "exe-format"])
        file.write(" -nographics")
        file.write(" -silentcrashes")
        file.write(" -automated")

        scene_log_argument = "-%s" % _clean_variation(project['path'])
        if scene:
            scene_log_argument = "-%s" % scene_name
        file.write(
            " -logfile " + "output-" + tag["name"] + scene_log_argument + "-build-" + backend + ".log")
        file.write(" -cleanedLogFile " + "output-" + tag["name"] + scene_log_argument + "-build-" + backend + "-cleaned.log")
        file.write(" -quit")
        if scene:
            file.write(" -scene " + scene["path"])
        file.write(" -scriptingBackend " + backend)
        file.write(" -displayResolutionDialog disabled\n")

        file.write("  artifacts:\n")
        file.write("    name: \"artifacts-" + tag["name"] + scene_log_argument + "\"\n")
        file.write("    when: always\n")
        file.write("    paths:\n")
        file.write("      - \"unity_revision.txt\"\n")
        file.write("      - \"*.log\"\n")
        file.write("      - \"*.xml\"\n")
        file.write("      - \"" + project["path"] + "/build\"\n")
        file.write("    expire_in: 1 week\n")
        if dependencyStage:
            file.write("  dependencies:\n")
            file.write(
                "    - " + name_prefix + tag["name"] + ":" + dependencyStage + ":" + _clean_variation(variation) + "\n")
            file.write("\n")
        file.write("\n")


class RunStage(IStage):
    _name="run"
    def generate(self, file, tag, variation, projects, backends, schedule_only, dependencyStage):
        for backend in backends:
            for project in projects:
                if self._name in project["skip-stage"]:
                    continue
                if 'schedule_only' in project and project['schedule_only'] and not schedule_only:
                    continue
                if 'scenes' in project:
                    for scene in project["scenes"]:
                        self.generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, scene)
                else:
                    self.generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, None)

    @staticmethod
    def generate_job(file, tag, variation, project, backend, schedule_only, dependencyStage, scene):
        name_prefix = ""
        if schedule_only:
            name_prefix = "schedule:"

        scene_name = ""
        scene_job_name = ":%s" % project['path']
        if scene:
            scene_name = scene['name']
            scene_job_name = ":%s" % scene['name']
        file.write(name_prefix + tag["name"] + ":run" + scene_job_name + ":" + backend + ":" + _clean_variation(
            variation) + ":\n")

        file.write("  before_script:\n")
        file.write("    - python Tools/CI/beforescript.py Player\n")

        file.write("  variables:\n    GIT_STRATEGY: fetch\n")
        file.write("  stage: run\n")
        if schedule_only:
            file.write("  only:\n")
        else:
            file.write("  except:\n")
        file.write("    - schedules\n")
        file.write("  tags:\n")
        for t in tag['tags']:
            file.write("  - %s\n" % t)

        file.write("  script:\n")
        file.write("    - " + tag["unity-launcher-player"])
        file.write(" -executable " + project["path"] + "/build/" + backend + "/" + tag["name"] + "-standalone" + tag[
            "exe-format"])

        scene_log_argument = "-%s" % _clean_variation(project['path'])
        if scene:
            scene_log_argument = "-%s" % scene_name
        file.write(
            " -logfile " + "output-" + tag["name"] + scene_log_argument + "-run-" + backend + ".log")
        file.write(" -cleanedLogFile " + "output-" + tag["name"] + scene_log_argument + "-run-" + backend + "-cleaned.log")
        if scene:
            file.write(" -enforceEmptyCleanedLogFile -timeout 120 -timeoutIgnore\n")
        else:
            file.write(" -enforceEmptyCleanedLogFile -timeout 1800\n")

        file.write("  artifacts:\n")
        file.write("    name: \"artifacts-" + tag["name"] + scene_log_argument + "\"\n")
        file.write("    when: always\n")
        file.write("    paths:\n")
        file.write("      - \"unity_revision.txt\"\n")
        file.write("      - \"*.log\"\n")
        file.write("      - \"*.xml\"\n")
        file.write("      - \"" + project["path"] + "/build\"\n")
        file.write("    expire_in: 1 week\n")
        if dependencyStage:
            file.write("  dependencies:\n")
            file.write("    - " + name_prefix + tag["name"] + ":" + dependencyStage + scene_job_name + ":" + backend + ":" + _clean_variation(variation) + "\n")
            file.write("\n")
        file.write("\n")
        pass


class TestStage(IStage):
    _name="test"
    def generate(self, file, tag, variation, projects, backends, schedule_only, dependencyStage):
        for project in projects:
            if self._name in project["skip-stage"]:
                continue
            name_prefix = ""
            if schedule_only:
                name_prefix = "schedule:"
            file.write(name_prefix + tag["name"] + ":test:" + _clean_variation(variation) + ":\n")
            file.write("  stage: test\n")
            file.write("  before_script:\n")
            file.write("    - python Tools/CI/beforescript.py Editor\n")
            file.write("  after_script:\n")
            file.write("    - python Tools/CI/afterscript.py\n")
            if schedule_only:
                file.write("  only:\n")
            else:
                file.write("  except:\n")
            file.write("    - schedules\n")
            file.write("  tags:\n")
            for t in tag['tags']:
                file.write("  - %s\n" % t)
            file.write("  script:\n")
            file.write("    - " + tag["unity-launcher-editor"])
            file.write(" -unityexecutable \"" + tag["unity-path"] + "\"")
            file.write(" -projectpath " + project["path"])
            file.write(" -batchmode")
            # TODO: Figure out why this one has started casuing test execution to fail if it is set
            #file.write(" -nographics")
            file.write(" -silentcrashes")
            file.write(" -automated")
            file.write(" -logfile " + "output-" + tag["name"] + "-test.log")
            file.write(" -runtests")
            file.write(" -testresults results.xml\n")

            file.write("  artifacts:\n")
            file.write("    name: \"artifacts-" + tag["name"] + "-test\"\n")
            file.write("    when: always\n")
            file.write("    paths:\n")
            file.write("      - \"unity_revision.txt\"\n")
            file.write("      - \"*.log\"\n")
            file.write("      - \"*.xml\"\n")
            file.write("      - \"" + project["path"] + "/build\"\n")
            file.write("    expire_in: 1 week\n")
            if dependencyStage:
                file.write("  dependencies:\n")
                file.write("    - " + name_prefix + dependencyStage + ":" +_clean_variation(variation) + "\n")
                file.write("\n")

            file.write("\n")


class ValidationStage():
    _name="validation"

    def generate(self, file, tag, variation, projects, backends, schedule_only, dependencyStage):
        name_prefix = ""
        if schedule_only:
            name_prefix = "schedule:"
        file.write(name_prefix + "validation:" + _clean_variation(variation) + ":\n")
        file.write("  stage: validation\n")
        file.write("  script:\n")
        file.write("    - python Tools/CI/validation.py --build-version '{0}' --project-path \"Samples\"\n".format(variation))
        if schedule_only:
            file.write("  only:\n")
        else:
            file.write("  except:\n")
        file.write("    - schedules\n")
        file.write("  tags:\n")
        file.write("  - darwin\n")
        file.write("  - buildfarm\n")
        file.write("  - 10.13.3\n")

        file.write("  artifacts:\n")
        file.write("    when: always\n")
        file.write("    paths:\n")
        file.write("      - \"unity_revision.txt\"\n")
        file.write("      - \"*.log\"\n")
        file.write("      - \"*.xml\"\n")
        file.write("    expire_in: 1 week\n")

        file.write("\n")

class StageFactory(object):
    def produce(stageName):
        if stageName == 'build':
            return BuildStage()
        elif stageName == 'run':
            return RunStage()
        elif stageName == 'test':
            return TestStage()
        elif stageName == 'validation':
            return ValidationStage()

    produce = staticmethod(produce)


def generateStages(file, stages):
    file.write("stages:\n")
    for stage in stages:
        file.write("  - " + stage + "\n")
    file.write("\n")


def generateJobs(file, configData):
    prevStage = ""

    for frequency, branches in configData["editorVariations"].iteritems():

        for variation in branches:
            generate_stage(file, {'name': 'macOS'}, 'validation', variation, configData, frequency, prevStage)
            prevStage = 'validation'
            for tag in configData["tags"]:
                for stage in configData["stages"]:
                    if stage == 'validation':
                        prevStage = 'validation'
                        continue
                    generate_stage(file, tag, stage, variation, configData, frequency, prevStage)
                    prevStage = stage
            # Validation is untagged so we just generate it once
            if prevStage == "validation":
                break


def generate_stage(file, tag, stage, variation, configData, frequency, prevStage):
    file.write("################################################################################\n")
    file.write("# " + tag["name"] + " " + stage + " for " + _clean_variation(variation) + "\n")
    file.write("################################################################################\n")

    schedule_only = False
    if frequency == "scheduled":
        schedule_only = True

    projects_to_generate = []
    if frequency == 'always':
        for project in configData["projects"]:
            projects_to_generate.append(project)
    else:
        projects_to_generate = configData["projects"]
    StageFactory.produce(stage).generate(file, tag, variation, projects_to_generate, configData["backends"],
                                         schedule_only, prevStage)


def generate(file, configData):
    generateStages(file, configData["stages"])
    generateJobs(file, configData)


def _clean_variation(variation):
    i = 0
    whitelisted = ['.', '_']
    variation = variation.replace('/', '_')
    while i < len(variation):
        if not variation[i].isalnum() and variation[i] not in whitelisted:
            trimmed = variation[:i].strip(".")
            return trimmed
        i += 1
    return variation

def main():
    jsonFile = open(os.path.join(os.path.dirname(os.path.realpath(__file__)), "file.json")).read()
    configData = json.loads(jsonFile)

    file=open(".gitlab-ci.yml","w")

    generate(file, configData)

    file.close()


main()
