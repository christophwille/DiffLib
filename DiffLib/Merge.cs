﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DiffLib
{
    internal class Merge<T> : IEnumerable<T?>
    {
        private readonly IMergeConflictResolver<T?> _ConflictResolver;
        private readonly List<DiffSection> _MergeSections;
        private readonly List<DiffElement<T?>> _DiffCommonBaseToLeft;
        private readonly List<DiffElement<T?>> _DiffCommonBaseToRight;

        public Merge(IList<T?> commonBase, IList<T?> left, IList<T?> right, IDiffElementAligner<T?> aligner, IMergeConflictResolver<T?> conflictResolver, IEqualityComparer<T?> comparer, DiffOptions diffOptions)
        {
            if (commonBase == null)
                throw new ArgumentNullException(nameof(commonBase));
            if (left == null)
                throw new ArgumentNullException(nameof(left));
            if (right == null)
                throw new ArgumentNullException(nameof(right));
            if (aligner == null)
                throw new ArgumentNullException(nameof(aligner));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (diffOptions == null)
                throw new ArgumentNullException(nameof(diffOptions));

            _ConflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));

            var diffCommonBaseToLeft = Diff.AlignElements(commonBase, left, Diff.CalculateSections(commonBase, left, diffOptions, comparer), aligner).ToList();
            _DiffCommonBaseToLeft = diffCommonBaseToLeft;

            var diffCommonBaseToRight = Diff.AlignElements(commonBase, right, Diff.CalculateSections(commonBase, right, diffOptions, comparer), aligner).ToList();
            _DiffCommonBaseToRight = diffCommonBaseToRight;

            var mergeSections = Diff.CalculateSections(diffCommonBaseToLeft!, diffCommonBaseToRight!, diffOptions, new DiffSectionMergeComparer<T>(comparer)).ToList();
            _MergeSections = mergeSections;
        }

        public IEnumerator<T?> GetEnumerator()
        {
            int leftIndex = 0;
            int rightIndex = 0;
            foreach (var section in _MergeSections)
            {
                if (section.IsMatch)
                {
                    for (int index = 0; index < section.LengthInCollection1; index++)
                        foreach (var item in ResolveMatchingElementFromBothSides(leftIndex++, rightIndex++))
                            yield return item;
                }
                else
                {
                    foreach (var item in ProcessNonMatchingElementsFromBothSides(section, rightIndex, leftIndex))
                        yield return item;

                    leftIndex += section.LengthInCollection1;
                    rightIndex += section.LengthInCollection2;
                }
            }
        }

        private IEnumerable<T?> ProcessNonMatchingElementsFromBothSides(DiffSection section, int rightIndex, int leftIndex)
        {
            if (section.LengthInCollection1 == 0)
            {
                // right side inserted, right side wins
                for (int index = 0; index < section.LengthInCollection2; index++)
                    yield return _DiffCommonBaseToRight[rightIndex + index].ElementFromCollection2.Value;
            }
            else if (section.LengthInCollection2 == 0)
            {
                // left side inserted, left side wins
                for (int index = 0; index < section.LengthInCollection1; index++)
                    yield return _DiffCommonBaseToLeft[leftIndex + index].ElementFromCollection2.Value;
            }
            else
            {
                var leftSide = new List<T?>();
                for (int index = 0; index < section.LengthInCollection1; index++)
                    leftSide.Add(_DiffCommonBaseToLeft[leftIndex + index].ElementFromCollection2.Value);

                var rightSide = new List<T?>();
                for (int index = 0; index < section.LengthInCollection2; index++)
                    rightSide.Add(_DiffCommonBaseToRight[rightIndex + index].ElementFromCollection2.Value);

                foreach (var item in _ConflictResolver.Resolve(new List<T?>(), leftSide, rightSide))
                    yield return item;
            }
        }

        private IEnumerable<T?> ResolveMatchingElementFromBothSides(int leftIndex, int rightIndex)
        {
            T? commonBase = _DiffCommonBaseToLeft[leftIndex].ElementFromCollection1.GetValueOrDefault();

            DiffOperation leftOp = _DiffCommonBaseToLeft[leftIndex].Operation;
            if (leftOp == DiffOperation.Replace)
                leftOp = DiffOperation.Modify;
            T? leftSide = _DiffCommonBaseToLeft[leftIndex].ElementFromCollection2.GetValueOrDefault();

            DiffOperation rightOp = _DiffCommonBaseToRight[rightIndex].Operation;
            if (rightOp == DiffOperation.Replace)
                rightOp = DiffOperation.Modify;
            T? rightSide = _DiffCommonBaseToRight[rightIndex].ElementFromCollection2.GetValueOrDefault();

            IEnumerable<T?> resolution = GetResolution(commonBase, leftOp, leftSide, rightOp, rightSide);
            return resolution;
        }

        private IEnumerable<T?> GetResolution(T? commonBase, DiffOperation leftOp, T? leftSide, DiffOperation rightOp, T? rightSide)
        {
            switch (leftOp)
            {
                case DiffOperation.Match:
                    switch (rightOp)
                    {
                        case DiffOperation.Match:
                            return new[] { leftSide };
                        case DiffOperation.Insert:
                            break;
                        case DiffOperation.Delete:
                            return Array.Empty<T>();
                        case DiffOperation.Replace:
                        case DiffOperation.Modify:
                            return new[] { rightSide };
                        default:
                            throw new ArgumentOutOfRangeException(nameof(rightOp), rightOp, null);
                    }

                    break;

                case DiffOperation.Insert:
                    break;

                case DiffOperation.Delete:
                    switch (rightOp)
                    {
                        case DiffOperation.Match:
                            return Array.Empty<T>();
                        case DiffOperation.Insert:
                            break;
                        case DiffOperation.Delete:
                            return Array.Empty<T>();
                        case DiffOperation.Replace:
                        case DiffOperation.Modify:
                            return _ConflictResolver.Resolve(new[] { commonBase }, new T[0], new[] { rightSide });
                        default:
                            throw new ArgumentOutOfRangeException(nameof(rightOp), rightOp, null);
                    }

                    break;

                case DiffOperation.Replace:
                case DiffOperation.Modify:
                    switch (rightOp)
                    {
                        case DiffOperation.Match:
                            return new[] { leftSide };
                        case DiffOperation.Insert:
                            break;
                        case DiffOperation.Delete:
                            return _ConflictResolver.Resolve(new[] { commonBase }, new[] { leftSide }, new T[0]);
                        case DiffOperation.Replace:
                        case DiffOperation.Modify:
                            return _ConflictResolver.Resolve(new[] { commonBase }, new[] { leftSide }, new[] { rightSide });
                        default:
                            throw new ArgumentOutOfRangeException(nameof(rightOp), rightOp, null);
                    }

                    break;
            }

            throw new MergeConflictException($"Unable to process {leftOp} vs. {rightOp}", new object?[] { commonBase }, new object?[] { leftSide }, new object?[] { rightSide });
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}