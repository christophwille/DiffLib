﻿using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

namespace DiffLib.Tests
{
    [TestFixture]
    public class MergeTests
    {
        [Test]
        [TestCase("", "", "", "", TestName = "Nothing to merge")]
        [TestCase("", "123", "", "123", TestName = "Only left side inserts")]
        [TestCase("", "", "123", "123", TestName = "Only right side inserts")]
        [TestCase("1234567890", "1234567890", "1234567890", "1234567890", TestName = "Nothing changed")]
        [TestCase("1234567890", "12a4567890", "123456b890", "12a456b890", TestName = "Both sides replaced to same")]
        [TestCase("1234567890", "123abc4567890", "1234567klm890", "123abc4567klm890", TestName = "Both sides inserted in separate places")]
        [TestCase("1234567890", "1234890", "1234567890", "1234890", TestName = "Left side deleted")]
        [TestCase("1234567890", "1234567890", "1234890", "1234890", TestName = "Right side deleted")]
        [TestCase("1234567890", "1234890", "1234890", "1234890", TestName = "Both sides deleted")]
        [TestCase("1234567890", "12abc34567890", "1234567890", "12abc34567890", TestName = "Left side inserted")]
        [TestCase("1234567890", "1234567890", "12abc34567890", "12abc34567890", TestName = "Right side inserted")]
        [TestCase("1234567890", "12abc34567890", "12klm34567890", "12abcklm34567890", TestName = "Both sides inserted at the same place, take left then right")]
        [TestCase("1234567890", "123abc7890", "1237890", "123abc7890", TestName = "Left side modified, right side deleted, take left side")]
        [TestCase("1234567890" ,"123567890", "123x567890", "123x567890", TestName = "Left side deleted, right side modified, take left then right")]
        [TestCase("1234567890", "123a567890", "123b567890", "123ab567890", TestName = "Both side modified, take left then right")]
        public void Perform_TestCases(string commonBase, string left, string right, string expected)
        {
            string output = new string(Merge.Perform(commonBase.ToCharArray(), left.ToCharArray(), right.ToCharArray(), new BasicReplaceInsertDeleteDiffElementAligner<char>(), new TakeLeftThenRightMergeConflictResolver<char>()).ToArray());
            Assert.That(output, Is.EqualTo(expected));
        }

        private class AbortIfConflictResolver<T> : IMergeConflictResolver<T>
        {
            public IEnumerable<T> Resolve(IList<T> commonBase, IList<T> left, IList<T> right)
            {
                throw new NotSupportedException();
            }
        }

        [Test]
        public void Perform_DistinctAdditions_ShouldNotProduceAConflict()
        {
            char[] common = "{}".ToCharArray();
            char[] left = "{a}".ToCharArray();
            char[] right = "{} {b}".ToCharArray();
            char[] expected = "{a} {b}".ToCharArray();

            var result = Merge.Perform(common, left, right, new DiffOptions { EnablePatienceOptimization = false }, new BasicReplaceInsertDeleteDiffElementAligner<char>(), new AbortIfConflictResolver<char>()).ToList();

            CollectionAssert.AreEqual(expected, result);
        }
    }
}
