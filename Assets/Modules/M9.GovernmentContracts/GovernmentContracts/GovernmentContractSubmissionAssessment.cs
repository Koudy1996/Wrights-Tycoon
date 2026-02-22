namespace WrightTycoon.Core.GovernmentContracts;

public sealed record GovernmentContractSubmissionAssessment(
    GovernmentContractSubmissionCandidate Candidate,
    GovernmentContractComplianceReport Compliance);
