import subprocess

known_packages = {
    'package1' : '0.0.1'
}

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
        raise subprocess.CalledProcessError
    if _registry_empty(registry):
        raise subprocess.CalledProcessError("{0} is not in the npm registry".format(package_name))

    if extra == 'version':
        return known_packages[package_name]

def npm_cmd(cmd, registry):
    print 'This is a mock'
    instruction, package_name, extra = cmd.split(' ')
    if instruction == 'view':
        return _view(package_name, extra, registry)


