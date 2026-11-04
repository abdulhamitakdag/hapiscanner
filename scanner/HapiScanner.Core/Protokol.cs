using System.Text.Json.Serialization;

namespace HapiScanner.Core;

public static class ScannerBilgisi
{
    public const string Surum = "0.1.5";
}

public sealed record ClaimIstegi(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("public_key")] string PublicKey,
    [property: JsonPropertyName("assurance_level")] string AssuranceLevel,
    [property: JsonPropertyName("scanner_version")] string ScannerVersion,
    [property: JsonPropertyName("ruleset_version")] string RulesetVersion);

public sealed record ClaimCevabi(
    [property: JsonPropertyName("verification_id")] string VerificationId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("nonce")] string Nonce,
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("new_device")] bool NewDevice,
    [property: JsonPropertyName("network_status")] string NetworkStatus,
    [property: JsonPropertyName("heartbeat_interval")] int HeartbeatInterval,
    [property: JsonPropertyName("hapicraft_endpoints")] List<string> HapicraftEndpoints,
    [property: JsonPropertyName("expires_at")] string ExpiresAt);

public sealed record HeartbeatIstegi(
    [property: JsonPropertyName("verification_id")] string VerificationId,
    [property: JsonPropertyName("nonce")] string Nonce,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("sequence_number")] long SequenceNumber,
    [property: JsonPropertyName("scan_stage")] string ScanStage,
    [property: JsonPropertyName("progress")] int Progress,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public sealed record HeartbeatCevabi(
    [property: JsonPropertyName("nonce")] string Nonce,
    [property: JsonPropertyName("server_time")] string ServerTime);

public sealed record KanitIstegi(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("evidence_type")] string EvidenceType,
    [property: JsonPropertyName("artifact_category")] string ArtifactCategory,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("currently_present")] bool CurrentlyPresent,
    [property: JsonPropertyName("rule_version")] string? RuleVersion = null,
    [property: JsonPropertyName("first_seen")] string? FirstSeen = null,
    [property: JsonPropertyName("last_seen")] string? LastSeen = null);

public sealed record RaporIstegi(
    [property: JsonPropertyName("verification_id")] string VerificationId,
    [property: JsonPropertyName("nonce")] string Nonce,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("scanner_version")] string ScannerVersion,
    [property: JsonPropertyName("ruleset_version")] string RulesetVersion,
    [property: JsonPropertyName("overall_result")] string OverallResult,
    [property: JsonPropertyName("report_hash")] string ReportHash,
    [property: JsonPropertyName("report_body")] string ReportBody,
    [property: JsonPropertyName("forensic_coverage")] string ForensicCoverage,
    [property: JsonPropertyName("runtime_coverage")] string RuntimeCoverage,
    [property: JsonPropertyName("evidence")] List<KanitIstegi> Evidence,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public sealed record RaporCevabi(
    [property: JsonPropertyName("report_id")] string ReportId,
    [property: JsonPropertyName("accepted")] bool Accepted);

public sealed record ApiHatasi(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message")] string Message);
