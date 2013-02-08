﻿namespace Protein_Hoarder
{
    public class PSM
    {
        public CsvFile CsvFile { get; set; }

        public int ScanNumber { get; set; }

        public double PValue { get; set; }

        public PSM(CsvFile csvFile, int scanNumber, double pvalue)
        {
            CsvFile = csvFile;
            ScanNumber = scanNumber;
            PValue = pvalue;
        }

        public override string ToString()
        {
            return string.Format("SN = {0} {1}", ScanNumber, CsvFile);
        }
    }
}