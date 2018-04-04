import unittest
from unittest import TestCase
from BumpVersion import BumpVersion
from publishStable import increase_version
from publishStable import validate_version

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
