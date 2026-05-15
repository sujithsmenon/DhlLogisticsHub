namespace DhlLogistics.Web.CommonFunctions
{
    public class SessionGlobalVars
    {
        public short FinancialYear { get; set; }
        public string AspnetUserId { get; set; } = string.Empty;
        public int UserId { get; set; }

        public string GetFinShort()
        {
            return FinancialYear.ToString().Substring(2) + (FinancialYear + 1).ToString().Substring(2);
        }

        public string GetFinYearAbbreviation()
        {
            return FinancialYear.ToString() + "-" + (FinancialYear + 1).ToString().Substring(2);
        }

        public DateTime FinStartDate => new DateTime(FinancialYear, 4, 1);
        public DateTime FinEndDate   => new DateTime(FinancialYear + 1, 3, 31);
    }
}
