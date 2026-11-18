using System;
using Margin.Core;
using NUnit.Framework;

namespace Margin.Tests
{
    public class AccountRulesTests
    {
        [TestCase("ana", null)]
        [TestCase("ana.lopez_26", null)]
        [TestCase("an", "At least 3 characters.")]
        [TestCase("abcdefghijklmnopq", "16 characters at most.")]
        [TestCase("Ana", "Only lowercase letters, numbers, dots and underscores.")]
        [TestCase("ana-lopez", "Only lowercase letters, numbers, dots and underscores.")]
        [TestCase("deleted_ab", "Pick another name.")]
        public void Username_Rules(string input, string expected)
        {
            Assert.AreEqual(expected, AccountRules.ValidateUsername(input));
        }

        [Test]
        public void Username_IsNormalizedFromWhatPeopleType()
        {
            Assert.AreEqual("analopez", AccountRules.NormalizeUsername("  @Ana Lopez "));
            Assert.AreEqual("", AccountRules.NormalizeUsername(null));
        }

        [Test]
        public void Username_SuggestionStaysWithinTheLimit()
        {
            var today = new DateTime(2026, 11, 13);
            Assert.AreEqual("ana26", AccountRules.SuggestAlternative("ana", today));
            var s = AccountRules.SuggestAlternative("abcdefghijklmnop", today);
            Assert.AreEqual(AccountRules.UsernameMax, s.Length);
            Assert.IsNull(AccountRules.ValidateUsername(s));
        }

        [Test]
        public void Email_And_Password()
        {
            Assert.IsNull(AccountRules.ValidateEmail(" ana@up.edu.mx "));
            Assert.AreEqual("The email is missing an @.", AccountRules.ValidateEmail("ana.up.edu"));
            Assert.AreEqual("Check the email domain.", AccountRules.ValidateEmail("ana@up"));
            Assert.AreEqual("At least 8 characters.", AccountRules.ValidatePassword("abc1"));
            Assert.AreEqual("Add at least one number.", AccountRules.ValidatePassword("abcdefgh"));
            Assert.IsNull(AccountRules.ValidatePassword("abcdefg1"));
            Assert.Less(AccountRules.PasswordStrength("abcdefg1"), AccountRules.PasswordStrength("Abcdefg1!"));
            Assert.AreEqual(4, AccountRules.PasswordStrength("Abcdefg1!"));
        }

        [Test]
        public void DisplayNames()
        {
            var named = new Profile { username = "ana", name = " Ana López " };
            var unnamed = new Profile { username = "ana", name = "" };
            Assert.AreEqual("Ana López", AccountRules.DisplayName(named));
            Assert.AreEqual("Ana", AccountRules.FirstName(named));
            Assert.AreEqual("@ana", AccountRules.DisplayName(unnamed));
            Assert.AreEqual("ana", AccountRules.FirstName(unnamed));
            Assert.AreEqual("A", AccountRules.Initial(unnamed));
            Assert.AreEqual("Reader", AccountRules.DisplayName(null));
        }

        [Test]
        public void CleanQuery_RemovesFilterSyntax()
        {
            Assert.AreEqual("ana  lo", AccountRules.CleanQuery("ana,(lo"));
            Assert.AreEqual("a.b_c", AccountRules.CleanQuery("a.b_c*"));
            Assert.AreEqual(40, AccountRules.CleanQuery(new string('x', 60)).Length);
        }

        [Test]
        public void ColorFor_IsStableAndFromThePalette()
        {
            // Same FNV-1a hash as the web, so a username gets the same color on both.
            Assert.AreEqual(AccountRules.ColorFor("ana"), AccountRules.ColorFor("ana"));
            StringAssert.StartsWith("#", AccountRules.ColorFor("someone"));
        }

        [Test]
        public void AuthErrors_AreReadable()
        {
            StringAssert.Contains("Log in instead", AccountRules.FriendlyAuthError("user_already_exists", "User already registered"));
            StringAssert.Contains("Wrong email or password", AccountRules.FriendlyAuthError("invalid_credentials", "Invalid login credentials"));
            StringAssert.Contains("Guest mode is switched off", AccountRules.FriendlyAuthError("anonymous_provider_disabled", "Anonymous sign-ins are disabled"));
            StringAssert.Contains("Too many attempts", AccountRules.FriendlyAuthError("over_email_send_rate_limit", ""));
            Assert.AreEqual("Something went wrong. Try again.", AccountRules.FriendlyAuthError(null, null));
        }

        [Test]
        public void InviteLinks_RoundTrip()
        {
            var link = InviteLink.Build("https://marginreads.vercel.app/app/", "ana.lopez");
            Assert.AreEqual("https://marginreads.vercel.app/app/?add=ana.lopez", link);
            Assert.IsTrue(InviteLink.TryParse(link, out var u));
            Assert.AreEqual("ana.lopez", u);
            Assert.IsTrue(InviteLink.TryParse("https://x.app/preview/?lang=en&add=Ana#top", out u));
            Assert.AreEqual("ana", u);
            Assert.IsFalse(InviteLink.TryParse("ana", out _));
            Assert.IsFalse(InviteLink.TryParse("https://x.app/?add=a", out _)); // too short to be a username
            Assert.AreEqual("https://x.app/?lang=en&add=ana", InviteLink.Build("https://x.app/?lang=en", "ana"));
        }
    }
}
