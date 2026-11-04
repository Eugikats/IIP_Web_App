export type DocumentActionState = {
  action: string;
  label: string;
  requiredPermission: string;
  reason?: string | null;
};

export type DocumentValidationWarning = {
  code: string;
  message: string;
  severity: string;
  fieldName?: string | null;
  expectedValue?: string | null;
  observedValue?: string | null;
};

export type DocumentFieldVerification = {
  name: string;
  extractedValue: string;
  effectiveValue: string;
  correctedValue?: string | null;
  confidenceScore?: number | null;
  verificationStatus: string;
  sensitivity: string;
  warnings: DocumentValidationWarning[];
  correctedAt?: string | null;
  correctionReference?: string | null;
};

export type DocumentItem = {
  documentId: string;
  caseId: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  documentType: string;
  customerDocumentTypeKey?: string | null;
  processingCapability?: string | null;
  isSuperseded?: boolean;
  processingStatus: string;
  confidenceScore: number;
  fields: DocumentFieldVerification[];
  validationWarnings: DocumentValidationWarning[];
  extractionApproved: boolean;
  uploadedAt: string;
  processedAt?: string | null;
  correlationId: string;
  previewUrl: string;
  /** SHA-256 of the original exactly as received (item 11); absent on documents stored before. */
  sha256?: string | null;
  /** "Evidence" is kept exactly as taken; "Document" is paperwork; absent when not filed in a slot. */
  slotKind?: string | null;
  /** A separate JPEG preview made for looking at (HEIC and large photos); the original is unchanged. */
  imagePreviewUrl?: string | null;
  /** The one PDF built from the pictures in this document's slot, and this file's page in it (item 12). */
  slotPdfUrl?: string | null;
  slotPdfPage?: number;
  slotPdfPageCount?: number;
  /** Build-loop item 14: the name it is shown under (the file as sent keeps originalFileName), what the
   *  recogniser made of it, and whether a person should decide where it belongs. */
  displayName?: string | null;
  needsReview?: boolean;
  reviewReason?: string | null;
  recognisedTypeKey?: string | null;
  recognitionConfidence?: number | null;
  recognitionReason?: string | null;
  filedByRecognition?: boolean;
  classifierDisagrees?: boolean;
  allowedActions: DocumentActionState[];
  blockedActions: DocumentActionState[];
};

export function documentContractLabel(value: string) {
  const label = value
    .replace(/_/g, " ")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
  return label.replace(/\b\w/g, (character) => character.toUpperCase());
}
