﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using CSMSL;
using CSMSL.Analysis.Identification;
using CSMSL.Chemistry;
using CSMSL.IO;
using CSMSL.IO.OMSSA;
using CSMSL.IO.Thermo;
using CSMSL.Proteomics;
using CSMSL.Util.Collections;
using LumenWorks.Framework.IO.Csv;

namespace Coon.Compass.FdrOptimizer
{
    public class CsvFile
    {
        public string FilePath { get; set; }

        public string RawFileName { get; set; }

        public CsvFile(string filePath)
        {
            FilePath = filePath;
            _data = new Dictionary<int, SortedMaxSizedContainer<PeptideSpectralMatch>>();
            PeptideSpectralMatches = new List<PeptideSpectralMatch>();
        }

        public List<PeptideSpectralMatch> PeptideSpectralMatches;

        public List<Peptide> Peptides; 

        public int PsmCount
        {
            get
            {
                return PeptideSpectralMatches.Count;
            }
        }
        
        public double SystematicPrecursorMassError { get; private set; }

        public double MaximumPrecursorMassError { get; private set; }

        public double ScoreThreshold { get; set; }

        public MassTolerance PrecursorMassToleranceThreshold { get; set; }

        private Dictionary<int, SortedMaxSizedContainer<PeptideSpectralMatch>> _data;
        
        public void Read(IEnumerable<Modification> fixedModifications, int numberOfTopHits = 1, bool higherScoresAreBetter = false)
        {
            _data.Clear();
           
            using(OmssaCsvPsmReader reader = new OmssaCsvPsmReader(FilePath))
            {
                reader.AddFixedModifications(fixedModifications);
                bool first = true;
                foreach (PeptideSpectralMatch psm in reader.ReadNextPsm())
                {
                    if (first)
                    {
                        RawFileName = psm.FileName.Split('.')[0];
                        first = false;
                    }

                    // Have we already processed a peptide for this scan number?
                    int scanNumber = psm.SpectrumNumber;
                    SortedMaxSizedContainer<PeptideSpectralMatch> peptides;
                    if (!_data.TryGetValue(scanNumber, out peptides))
                    {
                        peptides = new SortedMaxSizedContainer<PeptideSpectralMatch>(numberOfTopHits);
                        _data.Add(scanNumber, peptides);
                    }
                    peptides.Add(psm);
                }
            }

            PeptideSpectralMatches.Clear();
            foreach (SortedMaxSizedContainer<PeptideSpectralMatch> set in _data.Values)
            {
                PeptideSpectralMatches.AddRange(set);
            }

        }

        public void UpdatePsmInformation(MSDataFile dataFile, bool useMedian = true)
        {
            List<double> errors = new List<double>();
            MaximumPrecursorMassError = 0;
            int count = 0;
            using (StreamWriter writer = new StreamWriter(FilePath.Replace(".csv", "_precursor_errors.csv")))
            {
                writer.WriteLine("Precursor Mass Error(ppm),e-value,Decoy");
                foreach (KeyValuePair<int, SortedMaxSizedContainer<PeptideSpectralMatch>> kvp in _data)
                {
                    int scanNumber = kvp.Key;
                    SortedMaxSizedContainer<PeptideSpectralMatch> psms = kvp.Value;

                    double observedMZ = dataFile.GetPrecusorMz(scanNumber);
                    foreach (PeptideSpectralMatch psm in psms)
                    {
                        psm.IsolationMz = observedMZ;
                        double theoMass = Mass.MassFromMz(observedMZ, psm.Charge);
                        MassTolerance tolerancePPM = MassTolerance.CalculatePrecursorMassError(psm.MonoisotopicMass, theoMass);
                        writer.WriteLine(tolerancePPM.Value+","+ -Math.Log10(psm.Score) +","+ (psm.IsDecoy ? "1":"0"));
                        psm.PrecursorMassError = tolerancePPM;
                        errors.Add(tolerancePPM.Value);
                        double positive = Math.Abs(tolerancePPM.Value);
                        if (positive > MaximumPrecursorMassError)
                        {
                            MaximumPrecursorMassError = positive;
                        }
                        count++;
                    }
                }
            }
            if (useMedian)
            {
                int midIndex = count/2;
                errors.Sort();
                double medianError;
                if (count%2 == 0)
                {
                    // count is even, average two middle elements
                    double a = errors[midIndex - 1];
                    double b = errors[midIndex];
                    medianError = (a + b)/2.0;
                }
                else
                {
                    // count is odd, return the middle element
                    medianError = errors[midIndex];
                }
                SystematicPrecursorMassError = medianError;
            }
            else
            {
                SystematicPrecursorMassError = errors.Average();
            }
        }

        public void ReducePsms(IEqualityComparer<Peptide> comparer)
        {
            if (comparer == null)
            {
                Peptides = PeptideSpectralMatches.Select(psm => new Peptide(psm)).ToList();
                return;
            }
            Dictionary<Peptide, Peptide> peptides = new Dictionary<Peptide, Peptide>(comparer);
            foreach (PeptideSpectralMatch psm in PeptideSpectralMatches)
            {
                Peptide peptide = new Peptide(psm);
                Peptide realPeptide;
                if (peptides.TryGetValue(peptide, out realPeptide))
                {
                    realPeptide.AddPsm(psm);
                }
                else
                {
                    peptides.Add(peptide,peptide);
                }
            }
            Peptides = peptides.Values.ToList();
        }
    }
}