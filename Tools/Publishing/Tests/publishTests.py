import unittest
from unittest import TestCase
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
        self.assertEquals(increase_version('1.2.3', False, False, False, False), '1.2.3')

    def test_increase_version_NoPreviewFlagAndIncreasePreview_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3', False, False, False, True), '1.2.3-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, False, True, False), '1.2.4-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, False, True, True), '1.2.4-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMinorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, True, False, False), '1.3.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMinorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, True, False, True), '1.3.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMinorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, True, True, False), '1.3.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMinorAndPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', False, True, True, True), '1.3.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, False, False, False), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, False, False, True), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, False, True, False), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, False, True, True), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndMinorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, True, False, False), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndMinorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, True, False, True), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpMajorAndMinorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, True, True, False), '2.0.0-preview.1')

    def test_increase_version_NoPreviewFlagAndBumpAllVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3', True, True, True, True), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndDoNotBumpAnyVersion_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, False, False, False), '1.2.3-preview.2')

    def test_increase_version_PreviewFlagAndIncreasePreview_ReturnSameVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, False, False, True), '1.2.3-preview.3')

    def test_increase_version_PreviewFlagAndBumpPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, False, True, False), '1.2.4-preview.1')

    def test_increase_version_PreviewFlagAndBumpPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, False, True, True), '1.2.4-preview.1')

    def test_increase_version_PreviewFlagAndBumpMinorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, True, False, False), '1.3.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMinorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, True, False, True), '1.3.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMinorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, True, True, False), '1.3.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMinorAndPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', False, True, True, True), '1.3.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, False, False, False), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, False, False, True), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, False, True, False), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndPatchAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, False, True, True), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndMinorVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, True, False, False), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndMinorAndPreviewVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, True, False, True), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpMajorAndMinorAndPatchVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, True, True, False), '2.0.0-preview.1')

    def test_increase_version_PreviewFlagAndBumpAllVersion_ReturnNewVersion(self):
        self.assertEquals(increase_version('1.2.3-preview.2', True, True, True, True), '2.0.0-preview.1')







