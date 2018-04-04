class Enum(object):
    class __metaclass__(type):
        def __iter__(self):
            for item in self.__dict__:
                if item == self.__dict__[item]:
                    yield item

class BumpVersion(Enum):
    NONE    = 'NONE'
    RELEASE = 'RELEASE'
    PREVIEW = 'PREVIEW'
    PATCH   = 'PATCH'
    MINOR   = 'MINOR'
    MAJOR   = 'MAJOR'
