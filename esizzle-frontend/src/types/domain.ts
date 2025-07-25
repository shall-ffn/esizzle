// Core business domain types matching the API models

export interface Offering {
  offeringId: number
  offeringName?: string
  offeringDescription?: string
  dueDiligenceStart?: Date
  dueDiligenceEnd?: Date
  visible: boolean
  ddIsLive: boolean
  salesCount: number
  loansCount: number
}

export interface OfferingDetails extends Offering {
  offeringNumber?: string
  offeringHeader?: string
  bidDate?: Date
  clientId?: number
  orderBy: number
  offeringLogoUrl?: string
  offeringFolder?: string
  announcementHeader1?: string
  announcementHeader2?: string
  currentOfferingsHeader?: string
  currentOfferingsVisible?: boolean
  requiresPreQualified?: boolean
  confidentialityAgreementId: number
  secondConfidentialityAgreementId: number
  cutOffPackageAvailable: boolean
  activeForAccessLevel: number
  offeringStatusId?: number
  offeringTypeId?: number
  showOnHomepage?: boolean
  isServicer: boolean
  bidDateTimeZone?: string
  askAssetQuestions: boolean
  closingDate?: Date
  poolPrefix?: string
  autoGrantDD: boolean
  isTest?: boolean
  amount?: string
  isNotable: boolean
  showInPreviousOfferingsList: boolean
  canOrderHD: boolean
  useWebDD: boolean
  showImagesDisclaimer: boolean
  announceDate?: Date
  constellationProjectId?: number
  bidDepositAmount: number
  collectWireBidInfo: boolean
  ddUseDocFolders: boolean
  defaultLoanView: string
  forceAffiliateEntity: boolean
  allowComboBids: boolean
  dateArchived?: Date
  dynamicDataDownloadEnabled: boolean
  defaultLoanDataSort?: string
  bidTypeId: number
  indexCode: string
  expandSummaryDocFolders: boolean
  bucketPrefix?: string
  offlineArchiveDate?: Date
  showOfferingStatistics: boolean
  bpoProjectId?: number
  deleted: boolean
}

export interface Sale {
  saleId: number
  saleDesc: string
  saleNum?: string
  displayOrder?: number
  addsStartOn: Date
  bidDateOn?: Date
  loansCount: number
  imagingFolder?: string
}

export interface SaleDetails extends Sale {
  foxproDataDir?: string
  reportHeader1?: string
  reportHeader2?: string
  generatePostClosingReports: boolean
  sellerId: number
  saleTypeId: number
  initialStratOn?: Date
  interimStratOn?: Date
  cutoffOn?: Date
  allowsAllocatedComboBids: boolean
  scrapeDirectory?: string
  initialQcStartDate?: Date
  initialQcEndDate?: Date
  cutoffQcStartDate?: Date
  cutoffQcEndDate?: Date
  bidroomActive?: boolean
  usePecBidderInfo?: boolean
  clientId?: number
  funding1Date?: Date
  funding2Date?: Date
  allowDataUpload: boolean
  bankSign?: string
  bankTitle?: string
  bankName?: string
  bankName2?: string
  bankAddr1?: string
  bankAddr2?: string
  bankCity?: string
  bankState?: string
  bankZip?: string
  imageUploadCutoffDate?: Date
  useRules: boolean
  currentBidRoundId?: number
  saleFolder?: string
  maxBidPercentage?: number
  valuationSummary?: string
  offlineArchiveDate?: Date
  glacierArchiveId?: string
  allowDollarInput: boolean
}

export interface Loan {
  loanId: number
  assetNo?: string
  assetName: string
  assetName2?: string
  bookBalance?: number
  loadedOn: Date
  saleId: number
  loanStatusId: number
  documentCount: number
  lastDocumentDate?: Date
}

export interface LoanDetails extends Loan {
  accrualCode?: string
  accrualComment?: string
  accrualStatus?: string
  addCode?: string
  addComment?: string
  addedOn?: Date
  investorAddedOn?: Date
  addShippedOn?: Date
  allocatedPct?: number
  bank?: string
  box?: number
  chargedOffOn?: Date
  isChargedOff?: boolean
  costCode?: string
  costCenter?: number
  ficoScore?: string
  ficoScoreOn?: Date
  generalLedgerBank?: number
  hierarchy?: string
  appliedInterest?: number
  litigationReportOn?: Date
  netChargeOffAmt?: number
  netPct?: number
  netReturn?: number
  note?: string
  obligorId?: number
  officerNo?: string
  isOnStrat?: boolean
  poolId?: number
  poolIdPrevious?: number
  poolChangedOn?: Date
  pullCode?: string
  pullComment?: string
  pulledOn?: Date
  investorPulledOn?: Date
  pullShippedOn?: Date
  region?: string
  regionLead?: string
  reservePct?: number
  systemCode?: string
  underwrittenBy?: string
  valueLock?: number
  legalBalance: number
  officerName?: string
  interestRate?: number
  originalNoteAmt?: number
  originatedOn?: Date
  maturesOn?: Date
  currentNoteAmt?: number
  currentBankName?: string
  payoffAmt?: number
  payoffAsOf?: Date
  principleBalance?: number
  pastDueAmt?: number
  pastDueOn?: Date
  daysPastDue?: number
  loanType?: string
  isPerforming?: boolean
  isSecured?: boolean
  isUnderwritten?: boolean
  underwrittenOn?: Date
  ffnNo?: string
  collateralDescription?: string
  noteComments?: string
}

export interface DocumentSummary {
  id: number
  originalName: string
  documentType?: string
  pageCount?: number
  length: number
  dateCreated: Date
  dateUpdated?: Date
  imageStatusTypeId: number
  corrupted: boolean
  isRedacted?: boolean
  comments?: string
  loanId?: number
  assetNumber?: string
}

export interface DocumentDetails extends DocumentSummary {
  offeringId?: number
  documentDate?: Date
  supplementalDate?: Date
  documentFileDate?: Date
  path: string
  lockedByKey?: string
  sellerUniqueId?: string
  hasHardCopy: boolean
  optimized?: boolean
  deleted: boolean
  processOrder: number
  imageDocumentTypeId?: number
  originalExt: string
  nextDocumentStatusTypeId?: number
  qcDate?: Date
  qcUserId?: number
  ocrDate?: Date
  boxNumber?: string
  textExtractDate?: Date
  textMetricDate?: Date
  docTypeClient?: string
  docTypeManualId?: number
  docTypeAutoId?: number
  batchId?: string
  isWorkingFile?: boolean
  isSoftCopy?: boolean
  releaseDate?: Date
  fileHash?: string
  releaseImageType: string
  closingDoc: boolean
  hasDuplicate: boolean
  releaseUserId?: number
  overrideActions: boolean
  bucketPrefix?: string
  externalId?: number
}

export interface DocumentUrlResponse {
  url: string
  expiresAt: Date
  contentType: string
}

export interface RotateDocumentRequest {
  angle: number // 90, 180, 270
  pageNumber?: number // null for all pages
}

export interface RedactionArea {
  pageNumber: number
  x: number
  y: number
  width: number
  height: number
}

export interface RedactDocumentRequest {
  areas: RedactionArea[]
  permanentRedaction: boolean
}

export interface User {
  id: number
  name: string
  email: string
  userName: string
  systemRoles?: string[]
  clientId?: number
  accessLevel: number
}

// UI-specific types
export type ViewMode = 'single' | 'thumbnail'
export type PanelSizes = {
  left: number
  center: number
  right: number
}

export interface LoadingStates {
  offerings: boolean
  sales: boolean
  loans: boolean
  documents: boolean
  documentContent: boolean
}