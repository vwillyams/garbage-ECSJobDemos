import unittest
import mock
import argparse
import os
from unittest import TestCase
from BumpVersion import BumpVersion
from publishStable import args
from publishStable import get_packages_folder
from publishStable import get_list_of_packages
from publishStable import increase_version
from publishStable import validate_version
from publishStable import is_preview
import semver

class TestGetPackagesFolder(TestCase):
    def test_get_packages_folder_SetfolderWithoutManifest_RaiseException(self):
        args.packages_path = '.'
        self.assertRaises(Exception, get_packages_folder)

    def test_get_packages_folder_SetfolderWithManifest_ReturnAbsolutePath(self):
        args.packages_path = './packageContainer/'
        self.assertEqual(get_packages_folder(), os.path.abspath('./packageContainer/'))

class TestGetListOfPackages(TestCase):
    def test_get_list_of_packages_ProvideUnexistingFolder_ReturnEmptyList(self):
        self.assertEqual(get_list_of_packages('./SomeNonExistingFolder'), [])

    def test_get_list_of_packages_ProvideFolderWithoutPackages_ReturnEmptyList(self):
        self.assertEqual(get_list_of_packages('.'), [])

    def test_get_list_of_packages_ProvideFolderWithTwoPackages_ReturnList(self):
        projectPath = './packageContainer'
        expectedPackages = [os.path.join(projectPath, 'package1'), os.path.join(projectPath, 'package2')]
        self.assertEqual(get_list_of_packages(projectPath), expectedPackages)

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
