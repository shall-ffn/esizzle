namespace EsizzleAPI.Models;

public class LoanModel
{
    public int LoanId { get; set; }
    public string? AssetNo { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string? AssetName2 { get; set; }
    public string? AccrualCode { get; set; }
    public string? AccrualComment { get; set; }
    public string? AccrualStatus { get; set; }
    public string? AddCode { get; set; }
    public string? AddComment { get; set; }
    public DateTime? AddedOn { get; set; }
    public DateTime? InvestorAddedOn { get; set; }
    public DateTime? AddShippedOn { get; set; }
    public decimal? AllocatedPct { get; set; }
    public string? Bank { get; set; }
    public decimal? BookBalance { get; set; }
    public short? Box { get; set; }
    public DateTime? ChargedOffOn { get; set; }
    public bool? IsChargedOff { get; set; }
    public string? CostCode { get; set; }
    public long? CostCenter { get; set; }
    public string? FicoScore { get; set; }
    public DateTime? FicoScoreOn { get; set; }
    public short? GeneralLedgerBank { get; set; }
    public string? Hierarchy { get; set; }
    public decimal? AppliedInterest { get; set; }
    public DateTime? LitigationReportOn { get; set; }
    public DateTime LoadedOn { get; set; }
    public decimal? NetChargeOffAmt { get; set; }
    public decimal? NetPct { get; set; }
    public decimal? NetReturn { get; set; }
    public string? Note { get; set; }
    public long? ObligorId { get; set; }
    public string? OfficerNo { get; set; }
    public bool? IsOnStrat { get; set; }
    public int? PoolId { get; set; }
    public int? PoolIdPrevious { get; set; }
    public DateTime? PoolChangedOn { get; set; }
    public string? PullCode { get; set; }
    public string? PullComment { get; set; }
    public DateTime? PulledOn { get; set; }
    public DateTime? InvestorPulledOn { get; set; }
    public DateTime? PullShippedOn { get; set; }
    public string? Region { get; set; }
    public string? RegionLead { get; set; }
    public decimal? ReservePct { get; set; }
    public int SaleId { get; set; }
    public string? SystemCode { get; set; }
    public string? UnderwrittenBy { get; set; }
    public decimal? ValueLock { get; set; }
    public string? AutoPaymentCode { get; set; }
    public string? AgencyCode { get; set; }
    public string? AgencyName { get; set; }
    public string? AssetCode { get; set; }
    public int? DaysPastDue { get; set; }
    public bool? IsGovernmentGuaranteed { get; set; }
    public bool? IsGovernmentInsured { get; set; }
    public decimal? GovernmentGuaranteedPct { get; set; }
    public decimal? InterestAccrued { get; set; }
    public decimal? InterestBalance { get; set; }
    public string? InterestBasis { get; set; }
    public string? InterestComment { get; set; }
    public decimal? DailyInterestRate { get; set; }
    public string? InterestAccrualFrequency { get; set; }
    public DateTime? InterestPaidUpTo { get; set; }
    public decimal? InterestPaidYtd { get; set; }
    public decimal? InterestPastDue { get; set; }
    public DateTime? InterestPastDueOn { get; set; }
    public decimal? InterestRate { get; set; }
    public string? InterestType { get; set; }
    public DateTime? LastActivityOn { get; set; }
    public decimal? LastPaymentAmt { get; set; }
    public DateTime? LastPaymentOn { get; set; }
    public decimal? LateChargeAmt { get; set; }
    public decimal LegalBalance { get; set; }
    public decimal? LoanChargeAmt { get; set; }
    public DateTime? MaturesOn { get; set; }
    public string? MaturityType { get; set; }
    public DateTime? NextDueOn { get; set; }
    public decimal? OriginalNoteAmt { get; set; }
    public DateTime? OriginatedOn { get; set; }
    public DateTime? OriginalMaturesOn { get; set; }
    public decimal? OriginalInterestRate { get; set; }
    public string? OriginalNoteStatus { get; set; }
    public decimal? PayoffAmt { get; set; }
    public DateTime? PayoffAsOf { get; set; }
    public bool? IsPerforming { get; set; }
    public string? PrinAndIntType { get; set; }
    public decimal? PrincipleBalance { get; set; }
    public string? PrincipleFrequency { get; set; }
    public DateTime? PrinciplePaidTo { get; set; }
    public decimal? PrinciplePastDueAmt { get; set; }
    public DateTime? PrinciplePastDueOn { get; set; }
    public decimal? PastDueAmt { get; set; }
    public DateTime? PastDueOn { get; set; }
    public string? RiskRating { get; set; }
    public bool? IsSecured { get; set; }
    public bool? IsCorrespondenceStopped { get; set; }
    public long? TermOfLoan { get; set; }
    public decimal? UnfundedAmt { get; set; }
    public string? CashflowComment { get; set; }
    public bool? IsWithCollectionAgency { get; set; }
    public string? CollectionAgencyName { get; set; }
    public bool? IsUnderContingencyAgreement { get; set; }
    public string? ContingencyAgreementFirm { get; set; }
    public bool? IsDebtorDeceased { get; set; }
    public bool? IsDeficient { get; set; }
    public bool? IsForeclosed { get; set; }
    public bool? IsJudgement { get; set; }
    public decimal? CurrentNoteAmt { get; set; }
    public string? CurrentBankName { get; set; }
    public DateTime? CurrentNoteDate { get; set; }
    public string? CurrentIntRateType { get; set; }
    public DateTime? CurrentNoteMaturesOn { get; set; }
    public decimal? CurrentPaymentAmt { get; set; }
    public decimal? CurrentInterestRate { get; set; }
    public string? CurrentNoteStatus { get; set; }
    public bool? IsModified { get; set; }
    public DateTime? ModificationOn { get; set; }
    public string? ModificationStatus { get; set; }
    public string? NameOnNote { get; set; }
    public string? OriginalBankName { get; set; }
    public string? OriginalIntRateType { get; set; }
    public string? OriginalNameOnNote { get; set; }
    public decimal? OriginalPaymentAmt { get; set; }
    public bool? IsSecuredByCd { get; set; }
    public bool? IsSecuredByStock { get; set; }
    public bool? IsSecuredByTreasuryBill { get; set; }
    public bool? WasSecuredByIrb { get; set; }
    public string? OriginalInterestRateAdjustmentFrequency { get; set; }
    public string? InterestRateAdjustmentFrequency { get; set; }
    public bool? IsUnderwritten { get; set; }
    public DateTime? UnderwrittenOn { get; set; }
    public bool? WasSecuredBySba { get; set; }
    public bool? WasSecuredByLineOfCredit { get; set; }
    public bool? IsUnderSettlement { get; set; }
    public bool? IsSettlementInFile { get; set; }
    public bool? IsUnderForebearance { get; set; }
    public bool? IsForebearanceInFile { get; set; }
    public bool? IsSecuredBySba { get; set; }
    public bool? IsSecuredByLineOfCredit { get; set; }
    public bool? IsSecuredByIrb { get; set; }
    public bool? IsNoFutureCashFlow { get; set; }
    public string? InterestRateSpread { get; set; }
    public string? OriginalInterestRateSpread { get; set; }
    public string? OriginalInterestRateIndex { get; set; }
    public string? InterestRateIndex { get; set; }
    public decimal? OriginalInterestRateFloor { get; set; }
    public decimal? InterestRateFloor { get; set; }
    public decimal? OriginalInterestRateCap { get; set; }
    public decimal? InterestRateCap { get; set; }
    public decimal? OriginalInterestRateIncrementFloor { get; set; }
    public decimal? InterestRateIncrementFloor { get; set; }
    public decimal? OriginalInterestRateIncrementCap { get; set; }
    public decimal? InterestRateIncrementCap { get; set; }
    public DateTime? ImagingPrinted { get; set; }
    public string? PrincipleType { get; set; }
    public string? FfnNo { get; set; }
    public int? AmsNoteId { get; set; }
    public decimal? FfnReserveLow { get; set; }
    public decimal? FfnReserveHigh { get; set; }
    public decimal? BankReserveHigh { get; set; }
    public decimal? Valuation { get; set; }
    public int? PhaseId { get; set; }
    public int? OfficerId { get; set; }
    public long? BankClientNumber { get; set; }
    public string? CollateralDescription { get; set; }
    public DateTime? DataAsOf { get; set; }
    public string? BankruptcyChapter { get; set; }
    public DateTime? BankruptcyDate { get; set; }
    public decimal? EscrowBalance { get; set; }
    public string? LoanType { get; set; }
    public decimal? MortgageAmount { get; set; }
    public DateTime? MortgageDate { get; set; }
    public DateTime? MortgageMaturityDate { get; set; }
    public string? MortgageRecorded { get; set; }
    public string? OriginalNoteFixedVarInterest { get; set; }
    public string? PrePaidPenalty { get; set; }
    public int? InterestDaysPastDue { get; set; }
    public string? MortgageModification { get; set; }
    public int? RelatedTo { get; set; }
    public int LoanStatusId { get; set; } = 3;
    public string? OfficerName { get; set; }
    public decimal? PrinIntAmount { get; set; }
    public decimal? TaxIntAmount { get; set; }
    public decimal? TotalPaymentAmount { get; set; }
    public DateTime? BalloonDate { get; set; }
    public int? Late30 { get; set; }
    public int? Late60 { get; set; }
    public int? Late90 { get; set; }
    public decimal? GrossBalance { get; set; }
    public string? NoteComments { get; set; }
}

public class LoanSummaryModel
{
    public int LoanId { get; set; }
    public string? AssetNo { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string? AssetName2 { get; set; }
    public decimal? BookBalance { get; set; }
    public DateTime LoadedOn { get; set; }
    public int SaleId { get; set; }
    public int LoanStatusId { get; set; }
    public int DocumentCount { get; set; }
    public DateTime? LastDocumentDate { get; set; }
}