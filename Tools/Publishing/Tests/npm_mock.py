import subprocess
import json

def _registry_exists(registry):
    if registry.startswith('wrong'):
        return False
    return True

def _registry_empty(registry):
    if not registry.startswith("empty"):
        return False
    return True

def _view(package_name, extra, registry):
    if not _registry_exists(registry):
        raise subprocess.CalledProcessError(1,'view',"doh!")
    if _registry_empty(registry):
        raise subprocess.CalledProcessError(1,'view',"{0} is not in the npm registry".format(package_name))

    package_name = package_name.split('@')[0]
    file = open('./localPackages/' + package_name + '/package.json').read()
    fields = json.loads(file)

    if extra == 'version':
        return fields['version']

    if extra =='dependencies':

        str = '{ '
        for index, dependency_name in enumerate(fields['dependencies']):
            comma = ','
            if(index == len(fields['dependencies']) - 1):
                comma = ''
            str += "'" + dependency_name + "': '" + fields['dependencies'][dependency_name] + "'" + comma + "\n"
        str += '}'
        return str



def npm_cmd(cmd, registry):
    try:
        instruction, package_name, extra = cmd.split(' ')
        if instruction == 'view':
            return _view(package_name, extra, registry)

    except subprocess.CalledProcessError as e:
        raise e

