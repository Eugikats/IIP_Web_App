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
  processingStatus: string;
  confidenceScore: number;
  fields: DocumentFieldVerification[];
  validationWarnings: DocumentValidationWarning[];
  extractionApproved: boolean;
  uploadedAt: string;
  processedAt?: string | null;
  correlationId: string;
  previewUrl: string;
  allowedActions: DocumentActionState[];
  blockedActions: DocumentActionState[];
};

export function documentContractLabel(value: string) {
  return value
    .replace(/_/g, " ")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}
