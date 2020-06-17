﻿namespace UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor
{
    using Content;
    using Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Util;

    /// <summary>
    /// Nearest Neighbour Word Extractor.
    /// This implementation leverages bounding boxes.
    /// </summary>
    public class NearestNeighbourWordExtractor : IWordExtractor
    {
        /// <summary>
        /// Create an instance of Nearest Neighbour Word Extractor, <see cref="NearestNeighbourWordExtractor"/>.
        /// </summary>
        public static NearestNeighbourWordExtractor Instance { get; } = new NearestNeighbourWordExtractor();

        /// <summary>
        /// Get the words using default options values.
        /// </summary>
        /// <param name="letters">The page's letters to group into <see cref="Word"/>s.</param>
        /// <returns>The <see cref="Word"/>s generated by the nearest neighbour method.</returns>
        public IEnumerable<Word> GetWords(IReadOnlyList<Letter> letters)
        {
            return GetWords(letters, new NearestNeighbourWordExtractorOptions());
        }

        /// <summary>
        /// Get the words using options values.
        /// </summary>
        /// <param name="letters">The page's letters to group into <see cref="Word"/>s.</param>
        /// <param name="options">The <see cref="NearestNeighbourWordExtractorOptions"/> to use.</param>
        /// <returns>The <see cref="Word"/>s generated by the nearest neighbour method.</returns>
        public IEnumerable<Word> GetWords(IReadOnlyList<Letter> letters, DlaOptions options)
        {
            if (options is NearestNeighbourWordExtractorOptions nnOptions)
            {
                if (letters == null || letters.Count == 0)
                {
                    return EmptyArray<Word>.Instance;
                }

                if (nnOptions.GroupByOrientation)
                {
                    // axis aligned
                    List<Word> words = GetWords(
                        letters.Where(l => l.TextOrientation == TextOrientation.Horizontal).ToList(),
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasureAA, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism);

                    words.AddRange(GetWords(
                        letters.Where(l => l.TextOrientation == TextOrientation.Rotate270).ToList(),
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasureAA, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism));

                    words.AddRange(GetWords(
                        letters.Where(l => l.TextOrientation == TextOrientation.Rotate180).ToList(),
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasureAA, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism));

                    words.AddRange(GetWords(
                        letters.Where(l => l.TextOrientation == TextOrientation.Rotate90).ToList(),
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasureAA, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism));

                    // not axis aligned
                    words.AddRange(GetWords(
                        letters.Where(l => l.TextOrientation == TextOrientation.Other).ToList(),
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasure, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism));

                    return words;
                }
                else
                {
                    return GetWords(letters,
                        nnOptions.MaximumDistance, nnOptions.DistanceMeasure, nnOptions.FilterPivot,
                        nnOptions.Filter, nnOptions.MaxDegreeOfParallelism);
                }
            }
            else
            {
                throw new ArgumentException("Options provided must be of type " + nameof(NearestNeighbourWordExtractorOptions) + ".", nameof(options));
            }
        }

        /// <summary>
        /// Gets the words.
        /// </summary>
        /// <param name="letters">The letters in the page.</param>
        /// <param name="maxDistanceFunction">The function that determines the maximum distance between two letters (start and end base line points),
        /// e.g. Max(GlyphRectangle.Width) x 20%.
        /// <para>If the distance between the two letters is greater, a new word will be created.</para></param>
        /// <param name="distMeasure">The distance measure between two letters (start and end base line points),
        /// e.g. the Manhattan distance.</param>
		/// <param name="filterPivotFunction"></param>
        /// <param name="filterFunction">Function used to filter out connection between letters, e.g. check if the letters have the same color.
        /// <para>If the function returns false, a new word will be created.</para></param>
        /// <param name="maxDegreeOfParallelism">Sets the maximum number of concurrent tasks enabled.
        /// <para>A positive property value limits the number of concurrent operations to the set value.
        /// If it is -1, there is no limit on the number of concurrently running operations.</para></param>
        private List<Word> GetWords(IReadOnlyList<Letter> letters,
            Func<Letter, Letter, double> maxDistanceFunction, Func<PdfPoint, PdfPoint, double> distMeasure,
            Func<Letter, bool> filterPivotFunction,
            Func<Letter, Letter, bool> filterFunction, int maxDegreeOfParallelism)
        {
            if (letters == null || letters.Count == 0) return new List<Word>();

            var groupedLetters = Clustering.NearestNeighbours(letters,
                distMeasure, maxDistanceFunction,
                l => l.EndBaseLine, l => l.StartBaseLine,
                filterPivotFunction,
                filterFunction,
                maxDegreeOfParallelism).ToList();

            List<Word> words = new List<Word>();
            foreach (var g in groupedLetters)
            {
                words.Add(new Word(g));
            }

            return words;
        }

        /// <summary>
        /// Nearest neighbour word extractor options.
        /// </summary>
        public class NearestNeighbourWordExtractorOptions : DlaOptions
        {
            /// <summary>
            /// The maximum distance between two letters (start and end base line points) within the same word, as a function of the two letters.
            /// If the distance between the two letters is greater than this maximum, they will belong to different words.
            /// <para>Default value is 20% of the Max(Width, PointSize) of both letters. If <see cref="TextOrientation"/> is Other, this distance is doubled.</para>
            /// </summary>
            public Func<Letter, Letter, double> MaximumDistance { get; set; } = (l1, l2) =>
            {
                double maxDist = Math.Max(Math.Max(Math.Max(Math.Max(Math.Max(
                    Math.Abs(l1.GlyphRectangle.Width),
                    Math.Abs(l2.GlyphRectangle.Width)),
                    Math.Abs(l1.Width)),
                    Math.Abs(l2.Width)),
                    l1.PointSize), l2.PointSize) * 0.2;

                if (l1.TextOrientation == TextOrientation.Other || l2.TextOrientation == TextOrientation.Other)
                {
                    return 2.0 * maxDist;
                }
                return maxDist;
            };

            /// <summary>
            /// The default distance measure used between two letters (start and end base line points).
            /// <para>Default value is the Euclidean distance.</para>
            /// </summary>
            public Func<PdfPoint, PdfPoint, double> DistanceMeasure { get; set; } = Distances.Euclidean;

            /// <summary>
            /// The distance measure used between two letters (start and end base line points) with axis aligned <see cref="TextOrientation"/>.
            /// Only used if GroupByOrientation is set to true.
            /// <para>Default value is the Manhattan distance.</para>
            /// </summary>
            public Func<PdfPoint, PdfPoint, double> DistanceMeasureAA { get; set; } = Distances.Manhattan;

            /// <summary>
            /// Function used to filter out connection between letters, e.g. check if the letters have the same color.
            /// If the function returns false, letters will belong to different words.
            /// <para>Default value checks whether the neighbour is a white space or not. If it is the case, it returns false.</para>
            /// </summary>
            public Func<Letter, Letter, bool> Filter { get; set; } = (_, l2) => !string.IsNullOrWhiteSpace(l2.Value);

            /// <summary>
            /// Function used prior searching for the nearest neighbour. If return false, no search will be done.
            /// <para>Default value checks whether the current letter is a white space or not. If it is the case, it returns false and no search is done.</para>
            /// </summary>
            public Func<Letter, bool> FilterPivot { get; set; } = l => !string.IsNullOrWhiteSpace(l.Value);

            /// <summary>
            /// If true, letters will be grouped by <see cref="TextOrientation"/> before processing.
            /// The DistanceMeasureAA will be used on axis aligned letters, and the DistanceMeasure on others.
            /// If false, DistanceMeasure will be used for all letters and DistanceMeasureAA won't be used.
            /// <para>Default value is true.</para>
            /// </summary>
            public bool GroupByOrientation { get; set; } = true;
        }
    }
}