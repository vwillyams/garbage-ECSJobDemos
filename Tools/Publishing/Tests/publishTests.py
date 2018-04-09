import os
import mock
import npm_mock
from unittest import TestCase
from BumpVersion import BumpVersion
from publishStable import args
from publishStable import get_packages_folder
from publishStable import get_list_of_packages
from publishStable import _get_all_files_in_package
from publishStable import _get_version_in_registry
from publishStable import download_package_tarball
from publishStable import is_package_changed
from publishStable import cmp_files
from publishStable import increase_version
from publishStable import validate_version
from publishStable import parse_package_json
from publishStable import compare_package_files
from publishStable import compare_json_keys
from publishStable import is_preview
from publishStable import get_filtered_dependencies_from_view_registry
from publishStable import best_view_registry
from publishStable import parseArgumentList

import publishStable
import subprocess
import semver

class TestGetPackagesFolder(TestCase):
    def test_get_packages_folder_SetfolderWithoutManifest_RaiseException(self):
        args.packages_path = '.'
        self.assertRaises(Exception, get_packages_folder)

    def test_get_packages_folder_SetfolderWithManifest_ReturnAbsolutePath(self):
        args.packages_path = './localPackages/'
        self.assertEqual(get_packages_folder(), os.path.abspath('./localPackages/'))

class TestGetListOfPackages(TestCase):
    def test_get_list_of_packages_ProvideUnexistingFolder_ReturnEmptyList(self):
        self.assertEqual(get_list_of_packages('./SomeNonExistingFolder'), [])

    def test_get_list_of_packages_ProvideFolderWithoutPackages_ReturnEmptyList(self):
        self.assertEqual(get_list_of_packages('.'), [])

    def test_get_list_of_packages_ProvideFolderWithTwoPackages_ReturnList(self):
        projectPath = './localPackages'
        expectedPackages = [os.path.join(projectPath, 'package1'), os.path.join(projectPath, 'package2')]
        self.assertEqual(get_list_of_packages(projectPath), expectedPackages)

class TestParsePackageJson(TestCase):
    def test_parse_package_json_NonExistingPath_RaiseException(self):
        self.assertRaises(Exception, parse_package_json, './this/is/an/invalid/path')

    def test_parse_package_json_InvalidJSON_RaiseException(self):
        self.assertRaises(ValueError, parse_package_json, './installedPackages/brokenPackage')

    def test_parse_package_json_ValidJSON_RaiseException(self):
        package1Reference = {u'keywords': [u'test_package', u'unity'], u'version': u'0.0.1',
                             u'name': u'package1', u'dependencies': {u'package3': u'0.2.1'},
                             u'description': u'package1'}

        self.assertEqual(parse_package_json('./localPackages/package1'), package1Reference)

class TestCompareJsonKeys(TestCase):
    package1Reference = {u'keywords': [u'test_package', u'unity'], u'version': u'0.0.1',
                         u'name': u'package1', u'dependencies': {u'package3': u'0.2.1'},
                         u'description': u'package1'}
    package1ReferenceNoDependencies = {u'keywords': [u'test_package', u'unity'], u'version': u'0.0.1',
                         u'name': u'package1', u'description': u'package1'}
    emptyReference = {}

    number = 42

    def test_compare_json_keys_InvalidLocalPackage_RaiseException(self):
        self.assertRaises(AttributeError, compare_json_keys, self.number, self.package1Reference)

    def test_compare_json_keys_InvalidInstalledPackage_RaiseException(self):
        self.assertRaises(AttributeError, compare_json_keys, self.package1Reference,  self.number)

    def test_compare_json_keys_DifferentPackageDescriptors_ReturnFalse(self):
        self.assertFalse(compare_json_keys(self.package1Reference, self.package1ReferenceNoDependencies))

    def test_compare_json_keys_EqualPackageDescriptors_ReturnTrue(self):
        self.assertTrue(compare_json_keys(self.package1Reference, self.package1Reference))

class TestComparePackageFiles(TestCase):
    def test_compare_package_files_NoDifferencesInPackage_ReturnsTrue(self):
        self.assertTrue(compare_package_files('./localPackages/package1', './installedPackages/package1Clone', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONKeys_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package3', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONDependencyVersions_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentDependencyVersion', '0.0.1'))
    # TODO
    # def test_compare_package_files_DifferencesInPackageJSONDependencyPackage_ReturnsFalse(self):
    #     args.add_package_as_dependency_to_package = 'package2'
    #     self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentDependencyPackage', '0.0.1'))
    #
    # def test_compare_package_files_DifferencesInPackageJSONEmptyDependency_ReturnsFalse(self):
    #     self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithEmptyDependencies', '0.0.1'))
    #
    # def test_compare_package_files_DifferencesInPackageJSONDependencyNewPackage_ReturnsFalse(self):
    #     args.add_package_as_dependency_to_package = 'package48'
    #     publishStable.local_packages = {'package48': '0.2.1'}
    #     self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithNewDependencyPackage', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONName_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentName', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONVersion_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentVersion', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONKeywords_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentKeywords', '0.0.1'))

    def test_compare_package_files_DifferencesInPackageJSONDescription_ReturnsFalse(self):
        self.assertFalse(compare_package_files('./localPackages/package1', './installedPackages/package1cloneWithDifferentDescription', '0.0.1'))

class TestGetAllFiles(TestCase):
    def test_get_all_files_ProvideNonExistingPath_RaiseException(self):
        self.assertRaises(ValueError, _get_all_files_in_package, './this/is/a/fake/path')

    def test_get_all_files_ProvideExistingPathNotPackage_RaiseException(self):
        self.assertRaises(ValueError, _get_all_files_in_package, '.')

    def test_get_all_files_ProvideExistingPackageWithoutNodeModules_ReturnListOfFiles(self):
        self.assertEquals(_get_all_files_in_package('./localPackages/package1'), ['testFile.txt'])

    def test_get_all_files_ProvideExistingPackageWithNodeModules_ReturnListOfFiles(self):
        self.assertEquals(_get_all_files_in_package('./installedPackages/package5'), ['testFile.txt'])

class TestCmpFiles(TestCase):
    def test_cmp_files_CompareNonExistingFiles_RaiseException(self):
        self.assertRaises(IOError, cmp_files, 'file', 'file')

    def test_cmp_files_CompareExistingSameFile_ReturnTrue(self):
        self.assertTrue(cmp_files('./localPackages/manifest.json', './localPackages/manifest.json'))

    def test_cmp_files_CompareExistingDifferentFiles_ReturnFalse(self):
        self.assertFalse(cmp_files('./localPackages/package1/package.json', './localPackages/package2/package.json'))

class TestVersionInRegistry(TestCase):
    def setUp(self):
        patch = mock.patch('publishStable.npm_cmd')
        self.npm_mock = patch.start()
        self.npm_mock.side_effect = npm_mock.npm_cmd

    def tearDown(self):
        mock.patch.stopall()

    def test_mock_npm_cmd_ProvideExistingPackageInRegistry_ReturnExpectedVersion(self):
        self.assertEquals(_get_version_in_registry('package1', 'registry'), '0.0.1')

    def test_mock_npm_cmd_ProvideNonExistingPackageInExistingRegistry_ReturnEmptyVersion(self):
        self.assertEquals(_get_version_in_registry('package1', 'empty_registry'), '')

    def test_mock_npm_cmd_ProvideNonExistingRegistry_RaiseException(self):
        self.assertRaises(subprocess.CalledProcessError, _get_version_in_registry, 'package1', 'wrong_registry')

class TestIsPreview(TestCase):
    def test_is_preview_VersionIsStableRelease_ReturnFalse(self):
        self.assertFalse(is_preview(semver.parse_version_info('1.2.3')))

    def test_is_preview_VersionIsPreviewWithoutIndex_ReturnFalse(self):
        self.assertFalse(is_preview(semver.parse_version_info('1.2.3-preview')))

    def test_is_preview_VersionIsPreviewWithTypo_ReturnFalse(self):
        self.assertFalse(is_preview(semver.parse_version_info('1.2.3-preiew.2')))

    def test_is_preview_VersionIsPreviewWithoutDot_ReturnFalse(self):
        self.assertFalse(is_preview(semver.parse_version_info('1.2.3-preview2')))

    def test_is_preview_VersionIsPreview_ReturnTrue(self):
        self.assertTrue(is_preview(semver.parse_version_info('1.2.3-preview.2')))

class TestValidateVersion(TestCase):
    def test_validate_version_InvalidVersionFormatUsingAWord_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, 'testString')

    def test_validate_version_InvalidVersionFormatUsingThreeWords_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, 'testString.otherTest.lastTest')

    def test_validate_version_InvalidVersionFormatUsingTwoNumbers_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '2.5')

    def test_validate_version_InvalidVersionFormatUsingTypoOnPreview_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '1.2.3-peview.1')

    def test_validate_version_InvalidVersionFormatUsingCharacterWithNumbers_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '1.a.3')

    def test_validate_version_InvalidVersionFormatUsingCharacterInPreview_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '1.2.3-preview.a')

    def test_validate_version_InvalidVersionFormatUsingCharacterWithNumberInPreview_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '1.2.3-preview.1a')

    def test_validate_version_InvalidVersionFormatNoNumberInPreview_RaiseValueError(self):
        self.assertRaises(ValueError, validate_version, '1.2.3-preview')

    def test_validate_version_ValidVersion_DoesNotRaiseException(self):
        try:
            validate_version('1.2.3')
        except Exception as e:
            self.fail("validate_version raised an exception unexpectedly\n" + e.message)

    def test_validate_version_ValidVersionWithPreview_DoesNotRaiseException(self):
        try:
            validate_version('1.2.3-preview.2')
        except Exception as e:
            self.fail("validate_version raised an exception unexpectedly\n" + e.message)

class TestIncreaseVersion(TestCase):

    def test_increase_version_NoPreviewFlagAndDoNotBumpAnyVersion_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.NONE), '1.2.3')

    def test_increase_version_NoPreviewFlagAndIncreasePreview_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.PREVIEW), '1.2.3-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.PATCH), '1.2.4-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMinorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.MINOR), '1.3.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.MAJOR), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndRelease_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3', BumpVersion.RELEASE), '1.2.3')

    def test_increase_version_PreviewFlagAndDoNotBumpAnyVersion_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.NONE), '1.2.3-preview.2')

    def test_increase_version_PreviewFlagAndIncreasePreview_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.PREVIEW), '1.2.3-preview.3')

    def test_increase_version_PreviewFlagAndBumpPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.PATCH), '1.2.4-preview.1')

    def test_increase_version_PreviewFlagAndBumpMinorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.MINOR), '1.3.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.MAJOR), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndRelease_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', BumpVersion.RELEASE), '1.2.3')

class TestisPackageChanged(TestCase):
    pass

class TestGetFilteredDependenciesFromViewRegistry(TestCase):
    def setUp(self):
        patch = mock.patch('publishStable.npm_cmd')
        self.npm_mock = patch.start()
        self.npm_mock.side_effect = npm_mock.npm_cmd

    def tearDown(self):
        mock.patch.stopall()

    def test_get_filtered_dependencies_from_view_registry_ExistingPackageAndTrackedDependency_ReturnsDependencies(self):
        publishStable.best_view_registry = 'registry'
        publishStable.args.add_package_as_dependency_to_package = ['package1:package3']
        dependencies = {'package3' : '0.2.1'}
        self.assertEquals(get_filtered_dependencies_from_view_registry('package1', '0.0.1'), dependencies)

    def test_get_filtered_dependencies_from_view_registry_ExistingPackageAndNonTrackedDependency_ReturnsDependencies(self):
        publishStable.best_view_registry = 'registry'
        publishStable.args.add_package_as_dependency_to_package = ['package1:package2']
        self.assertEquals(get_filtered_dependencies_from_view_registry('package1', '0.0.1'), {})
