using GraphLedger.Core.Models.Utcm;

namespace GraphLedger.Core.Graph;

/// <summary>
/// Registry of UTCM resource types organized by workload.
/// Provides mappings between workload names and their associated UTCM resource types.
/// </summary>
public static class UtcmResourceTypeRegistry
{
    /// <summary>
    /// Mapping of workload names to their UTCM resource types.
    /// </summary>
    private static readonly Dictionary<string, List<UtcmResourceType>> WorkloadMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Entra"] =
        [
            new() { TypeName = "microsoft.entra.administrativeUnit", Workload = "Entra", FriendlyName = "Administrative Unit" },
            new() { TypeName = "microsoft.entra.application", Workload = "Entra", FriendlyName = "Application" },
            new() { TypeName = "microsoft.entra.authenticationContextClassReference", Workload = "Entra", FriendlyName = "Authentication Context Class Reference" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicy", Workload = "Entra", FriendlyName = "Authentication Method Policy" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyAuthenticator", Workload = "Entra", FriendlyName = "Authentication Method Policy - Authenticator" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyEmail", Workload = "Entra", FriendlyName = "Authentication Method Policy - Email" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyFido2", Workload = "Entra", FriendlyName = "Authentication Method Policy - FIDO2" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicySms", Workload = "Entra", FriendlyName = "Authentication Method Policy - SMS" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicySoftware", Workload = "Entra", FriendlyName = "Authentication Method Policy - Software" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyTemporary", Workload = "Entra", FriendlyName = "Authentication Method Policy - Temporary" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyVoice", Workload = "Entra", FriendlyName = "Authentication Method Policy - Voice" },
            new() { TypeName = "microsoft.entra.authenticationMethodPolicyX509", Workload = "Entra", FriendlyName = "Authentication Method Policy - X509" },
            new() { TypeName = "microsoft.entra.authenticationStrengthPolicy", Workload = "Entra", FriendlyName = "Authentication Strength Policy" },
            new() { TypeName = "microsoft.entra.authorizationPolicy", Workload = "Entra", FriendlyName = "Authorization Policy" },
            new() { TypeName = "microsoft.entra.conditionalAccessPolicy", Workload = "Entra", FriendlyName = "Conditional Access Policy" },
            new() { TypeName = "microsoft.entra.crossTenantAccessPolicy", Workload = "Entra", FriendlyName = "Cross-Tenant Access Policy" },
            new() { TypeName = "microsoft.entra.crossTenantAccessPolicyConfigurationDefault", Workload = "Entra", FriendlyName = "Cross-Tenant Access Policy - Default" },
            new() { TypeName = "microsoft.entra.crossTenantAccessPolicyConfigurationPartner", Workload = "Entra", FriendlyName = "Cross-Tenant Access Policy - Partner" },
            new() { TypeName = "microsoft.entra.entitlementManagementAccessPackage", Workload = "Entra", FriendlyName = "Entitlement Management - Access Package" },
            new() { TypeName = "microsoft.entra.entitlementManagementAccessPackageAssignmentPolicy", Workload = "Entra", FriendlyName = "Entitlement Management - Assignment Policy" },
            new() { TypeName = "microsoft.entra.entitlementManagementAccessPackageCatalog", Workload = "Entra", FriendlyName = "Entitlement Management - Catalog" },
            new() { TypeName = "microsoft.entra.entitlementManagementAccessPackageCatalogResource", Workload = "Entra", FriendlyName = "Entitlement Management - Catalog Resource" },
            new() { TypeName = "microsoft.entra.entitlementManagementConnectedOrganization", Workload = "Entra", FriendlyName = "Entitlement Management - Connected Organization" },
            new() { TypeName = "microsoft.entra.externalIdentityPolicy", Workload = "Entra", FriendlyName = "External Identity Policy" },
            new() { TypeName = "microsoft.entra.group", Workload = "Entra", FriendlyName = "Group" }
        ],
        ["Exchange"] =
        [
            new() { TypeName = "microsoft.exchange.acceptedDomain", Workload = "Exchange", FriendlyName = "Accepted Domain" },
            new() { TypeName = "microsoft.exchange.activeSyncDeviceAccessRule", Workload = "Exchange", FriendlyName = "ActiveSync Device Access Rule" },
            new() { TypeName = "microsoft.exchange.antiPhishPolicy", Workload = "Exchange", FriendlyName = "Anti-Phish Policy" },
            new() { TypeName = "microsoft.exchange.antiPhishRule", Workload = "Exchange", FriendlyName = "Anti-Phish Rule" },
            new() { TypeName = "microsoft.exchange.applicationAccessPolicy", Workload = "Exchange", FriendlyName = "Application Access Policy" },
            new() { TypeName = "microsoft.exchange.atpPolicyForO365", Workload = "Exchange", FriendlyName = "ATP Policy for O365" },
            new() { TypeName = "microsoft.exchange.authenticationPolicy", Workload = "Exchange", FriendlyName = "Authentication Policy" },
            new() { TypeName = "microsoft.exchange.authenticationPolicyAssignment", Workload = "Exchange", FriendlyName = "Authentication Policy Assignment" },
            new() { TypeName = "microsoft.exchange.availabilityAddressSpace", Workload = "Exchange", FriendlyName = "Availability Address Space" },
            new() { TypeName = "microsoft.exchange.availabilityConfig", Workload = "Exchange", FriendlyName = "Availability Config" },
            new() { TypeName = "microsoft.exchange.calendarProcessing", Workload = "Exchange", FriendlyName = "Calendar Processing" },
            new() { TypeName = "microsoft.exchange.casMailboxPlan", Workload = "Exchange", FriendlyName = "CAS Mailbox Plan" },
            new() { TypeName = "microsoft.exchange.casMailboxSettings", Workload = "Exchange", FriendlyName = "CAS Mailbox Settings" },
            new() { TypeName = "microsoft.exchange.dataClassification", Workload = "Exchange", FriendlyName = "Data Classification" },
            new() { TypeName = "microsoft.exchange.dataEncryptionPolicy", Workload = "Exchange", FriendlyName = "Data Encryption Policy" },
            new() { TypeName = "microsoft.exchange.distributionGroup", Workload = "Exchange", FriendlyName = "Distribution Group" },
            new() { TypeName = "microsoft.exchange.dkimSigningConfig", Workload = "Exchange", FriendlyName = "DKIM Signing Config" },
            new() { TypeName = "microsoft.exchange.emailAddressPolicy", Workload = "Exchange", FriendlyName = "Email Address Policy" },
            new() { TypeName = "microsoft.exchange.groupSettings", Workload = "Exchange", FriendlyName = "Group Settings" },
            new() { TypeName = "microsoft.exchange.hostedConnectionFilterPolicy", Workload = "Exchange", FriendlyName = "Hosted Connection Filter Policy" },
            new() { TypeName = "microsoft.exchange.hostedContentFilterPolicy", Workload = "Exchange", FriendlyName = "Hosted Content Filter Policy" }
        ],
        ["Intune"] =
        [
            new() { TypeName = "microsoft.intune.accountProtectionLocalUserGroupMembershipPolicy", Workload = "Intune", FriendlyName = "Account Protection - Local User Group Membership" },
            new() { TypeName = "microsoft.intune.accountProtectionPolicy", Workload = "Intune", FriendlyName = "Account Protection Policy" },
            new() { TypeName = "microsoft.intune.antivirusPolicyWindows10SettingCatalog", Workload = "Intune", FriendlyName = "Antivirus Policy - Windows 10 Setting Catalog" },
            new() { TypeName = "microsoft.intune.appConfigurationPolicy", Workload = "Intune", FriendlyName = "App Configuration Policy" },
            new() { TypeName = "microsoft.intune.applicationControlPolicyWindows10", Workload = "Intune", FriendlyName = "Application Control Policy - Windows 10" },
            new() { TypeName = "microsoft.intune.appProtectionPolicyAndroid", Workload = "Intune", FriendlyName = "App Protection Policy - Android" },
            new() { TypeName = "microsoft.intune.appProtectionPolicyiOS", Workload = "Intune", FriendlyName = "App Protection Policy - iOS" },
            new() { TypeName = "microsoft.intune.attackSurfaceReductionRulesPolicyWindows10ConfigManager", Workload = "Intune", FriendlyName = "Attack Surface Reduction Rules - Windows 10 ConfigMgr" },
            new() { TypeName = "microsoft.intune.deviceAndAppManagementAssignmentFilter", Workload = "Intune", FriendlyName = "Device and App Management Assignment Filter" },
            new() { TypeName = "microsoft.intune.deviceCategory", Workload = "Intune", FriendlyName = "Device Category" },
            new() { TypeName = "microsoft.intune.deviceCleanupRule", Workload = "Intune", FriendlyName = "Device Cleanup Rule" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicyAndroid", Workload = "Intune", FriendlyName = "Device Compliance Policy - Android" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicyAndroidDeviceOwner", Workload = "Intune", FriendlyName = "Device Compliance Policy - Android Device Owner" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicyAndroidWorkProfile", Workload = "Intune", FriendlyName = "Device Compliance Policy - Android Work Profile" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicyiOS", Workload = "Intune", FriendlyName = "Device Compliance Policy - iOS" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicymacOS", Workload = "Intune", FriendlyName = "Device Compliance Policy - macOS" },
            new() { TypeName = "microsoft.intune.deviceCompliancePolicyWindows10", Workload = "Intune", FriendlyName = "Device Compliance Policy - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationAdministrativeTemplatePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Administrative Template - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationCustomPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Custom Policy - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationDefenderForEndpointOnboardingPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Defender Onboarding - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationDeliveryOptimizationPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Delivery Optimization - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationDomainJoinPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Domain Join - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationEmailProfilePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Email Profile - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationEndpointProtectionPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Endpoint Protection - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationFirmwareInterfacePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Firmware Interface - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationHealthMonitoringConfigurationPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Health Monitoring - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationIdentityProtectionPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Identity Protection - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationImportedPfxCertificatePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Imported PFX Certificate - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationKioskPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Kiosk - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationNetworkBoundaryPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Network Boundary - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPkcsCertificatePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - PKCS Certificate - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyAndroidDeviceAdministrator", Workload = "Intune", FriendlyName = "Device Configuration Policy - Android Device Administrator" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyAndroidDeviceOwner", Workload = "Intune", FriendlyName = "Device Configuration Policy - Android Device Owner" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyAndroidOpenSourceProject", Workload = "Intune", FriendlyName = "Device Configuration Policy - Android Open Source Project" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyAndroidWorkProfile", Workload = "Intune", FriendlyName = "Device Configuration Policy - Android Work Profile" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyiOS", Workload = "Intune", FriendlyName = "Device Configuration Policy - iOS" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicymacOS", Workload = "Intune", FriendlyName = "Device Configuration Policy - macOS" },
            new() { TypeName = "microsoft.intune.deviceConfigurationPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration Policy - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationScepCertificatePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - SCEP Certificate - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationSecureAssessmentPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Secure Assessment - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationSharedMultiDevicePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Shared Multi-Device - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationTrustedCertificatePolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Trusted Certificate - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationVpnPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - VPN - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationWindowsTeamPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Windows Team - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceConfigurationWiredNetworkPolicyWindows10", Workload = "Intune", FriendlyName = "Device Configuration - Wired Network - Windows 10" },
            new() { TypeName = "microsoft.intune.deviceEnrollmentLimitRestriction", Workload = "Intune", FriendlyName = "Device Enrollment Limit Restriction" },
            new() { TypeName = "microsoft.intune.deviceEnrollmentPlatformRestriction", Workload = "Intune", FriendlyName = "Device Enrollment Platform Restriction" },
            new() { TypeName = "microsoft.intune.deviceEnrollmentStatusPageWindows10", Workload = "Intune", FriendlyName = "Device Enrollment Status Page - Windows 10" },
            new() { TypeName = "microsoft.intune.endpointDetectionAndResponsePolicyWindows10", Workload = "Intune", FriendlyName = "Endpoint Detection and Response - Windows 10" },
            new() { TypeName = "microsoft.intune.exploitProtectionPolicyWindows10SettingCatalog", Workload = "Intune", FriendlyName = "Exploit Protection - Windows 10 Setting Catalog" },
            new() { TypeName = "microsoft.intune.policySets", Workload = "Intune", FriendlyName = "Policy Sets" },
            new() { TypeName = "microsoft.intune.roleAssignment", Workload = "Intune", FriendlyName = "Role Assignment" },
            new() { TypeName = "microsoft.intune.roleDefinition", Workload = "Intune", FriendlyName = "Role Definition" },
            new() { TypeName = "microsoft.intune.settingCatalogAsrRulesPolicyWindows10", Workload = "Intune", FriendlyName = "Setting Catalog - ASR Rules - Windows 10" },
            new() { TypeName = "microsoft.intune.settingCatalogCustomPolicyWindows10", Workload = "Intune", FriendlyName = "Setting Catalog - Custom Policy - Windows 10" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyAndroidDeviceAdministrator", Workload = "Intune", FriendlyName = "WiFi Configuration - Android Device Administrator" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyAndroidEnterpriseDeviceOwner", Workload = "Intune", FriendlyName = "WiFi Configuration - Android Enterprise Device Owner" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyAndroidEnterpriseWorkProfile", Workload = "Intune", FriendlyName = "WiFi Configuration - Android Enterprise Work Profile" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyAndroidForWork", Workload = "Intune", FriendlyName = "WiFi Configuration - Android for Work" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyAndroidOpenSourceProject", Workload = "Intune", FriendlyName = "WiFi Configuration - Android Open Source Project" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyiOS", Workload = "Intune", FriendlyName = "WiFi Configuration - iOS" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicymacOS", Workload = "Intune", FriendlyName = "WiFi Configuration - macOS" },
            new() { TypeName = "microsoft.intune.wifiConfigurationPolicyWindows10", Workload = "Intune", FriendlyName = "WiFi Configuration - Windows 10" },
            new() { TypeName = "microsoft.intune.windowsAutopilotDeploymentProfileAzureADHybridJoined", Workload = "Intune", FriendlyName = "Windows Autopilot - Azure AD Hybrid Joined" },
            new() { TypeName = "microsoft.intune.windowsAutopilotDeploymentProfileAzureADJoined", Workload = "Intune", FriendlyName = "Windows Autopilot - Azure AD Joined" },
            new() { TypeName = "microsoft.intune.windowsInformationProtectionPolicyWindows10MdmEnrolled", Workload = "Intune", FriendlyName = "Windows Information Protection - MDM Enrolled" },
            new() { TypeName = "microsoft.intune.windowsUpdateForBusinessFeatureUpdateProfileWindows10", Workload = "Intune", FriendlyName = "Windows Update for Business - Feature Update" },
            new() { TypeName = "microsoft.intune.windowsUpdateForBusinessRingUpdateProfileWindows10", Workload = "Intune", FriendlyName = "Windows Update for Business - Ring Update" }
        ],
        ["Teams"] =
        [
            new() { TypeName = "microsoft.teams.appPermissionPolicy", Workload = "Teams", FriendlyName = "App Permission Policy" },
            new() { TypeName = "microsoft.teams.appSetupPolicy", Workload = "Teams", FriendlyName = "App Setup Policy" },
            new() { TypeName = "microsoft.teams.audioConferencingPolicy", Workload = "Teams", FriendlyName = "Audio Conferencing Policy" },
            new() { TypeName = "microsoft.teams.callHoldPolicy", Workload = "Teams", FriendlyName = "Call Hold Policy" },
            new() { TypeName = "microsoft.teams.callingPolicy", Workload = "Teams", FriendlyName = "Calling Policy" },
            new() { TypeName = "microsoft.teams.callParkPolicy", Workload = "Teams", FriendlyName = "Call Park Policy" },
            new() { TypeName = "microsoft.teams.callQueue", Workload = "Teams", FriendlyName = "Call Queue" },
            new() { TypeName = "microsoft.teams.channelsPolicy", Workload = "Teams", FriendlyName = "Channels Policy" },
            new() { TypeName = "microsoft.teams.clientConfiguration", Workload = "Teams", FriendlyName = "Client Configuration" },
            new() { TypeName = "microsoft.teams.complianceRecordingPolicy", Workload = "Teams", FriendlyName = "Compliance Recording Policy" },
            new() { TypeName = "microsoft.teams.cortanaPolicy", Workload = "Teams", FriendlyName = "Cortana Policy" },
            new() { TypeName = "microsoft.teams.dialInConferencingTenantSettings", Workload = "Teams", FriendlyName = "Dial-In Conferencing Tenant Settings" },
            new() { TypeName = "microsoft.teams.emergencyCallingPolicy", Workload = "Teams", FriendlyName = "Emergency Calling Policy" },
            new() { TypeName = "microsoft.teams.emergencyCallRoutingPolicy", Workload = "Teams", FriendlyName = "Emergency Call Routing Policy" },
            new() { TypeName = "microsoft.teams.enhancedEncryptionPolicy", Workload = "Teams", FriendlyName = "Enhanced Encryption Policy" },
            new() { TypeName = "microsoft.teams.eventsPolicy", Workload = "Teams", FriendlyName = "Events Policy" },
            new() { TypeName = "microsoft.teams.federationConfiguration", Workload = "Teams", FriendlyName = "Federation Configuration" },
            new() { TypeName = "microsoft.teams.feedbackPolicy", Workload = "Teams", FriendlyName = "Feedback Policy" },
            new() { TypeName = "microsoft.teams.filesPolicy", Workload = "Teams", FriendlyName = "Files Policy" },
            new() { TypeName = "microsoft.teams.groupPolicyAssignment", Workload = "Teams", FriendlyName = "Group Policy Assignment" },
            new() { TypeName = "microsoft.teams.guestCallingConfiguration", Workload = "Teams", FriendlyName = "Guest Calling Configuration" },
            new() { TypeName = "microsoft.teams.guestMeetingConfiguration", Workload = "Teams", FriendlyName = "Guest Meeting Configuration" },
            new() { TypeName = "microsoft.teams.guestMessagingConfiguration", Workload = "Teams", FriendlyName = "Guest Messaging Configuration" },
            new() { TypeName = "microsoft.teams.ipPhonePolicy", Workload = "Teams", FriendlyName = "IP Phone Policy" },
            new() { TypeName = "microsoft.teams.meetingBroadcastConfiguration", Workload = "Teams", FriendlyName = "Meeting Broadcast Configuration" },
            new() { TypeName = "microsoft.teams.meetingBroadcastPolicy", Workload = "Teams", FriendlyName = "Meeting Broadcast Policy" },
            new() { TypeName = "microsoft.teams.meetingConfiguration", Workload = "Teams", FriendlyName = "Meeting Configuration" },
            new() { TypeName = "microsoft.teams.meetingPolicy", Workload = "Teams", FriendlyName = "Meeting Policy" },
            new() { TypeName = "microsoft.teams.messagingPolicy", Workload = "Teams", FriendlyName = "Messaging Policy" }
        ],
        ["SecurityAndCompliance"] =
        [
            new() { TypeName = "microsoft.securityandcompliance.autoSensitivityLabelPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Auto Sensitivity Label Policy" },
            new() { TypeName = "microsoft.securityandcompliance.caseHoldPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Case Hold Policy" },
            new() { TypeName = "microsoft.securityandcompliance.caseHoldRule", Workload = "SecurityAndCompliance", FriendlyName = "Case Hold Rule" },
            new() { TypeName = "microsoft.securityandcompliance.complianceCase", Workload = "SecurityAndCompliance", FriendlyName = "Compliance Case" },
            new() { TypeName = "microsoft.securityandcompliance.complianceSearch", Workload = "SecurityAndCompliance", FriendlyName = "Compliance Search" },
            new() { TypeName = "microsoft.securityandcompliance.complianceSearchAction", Workload = "SecurityAndCompliance", FriendlyName = "Compliance Search Action" },
            new() { TypeName = "microsoft.securityandcompliance.complianceTag", Workload = "SecurityAndCompliance", FriendlyName = "Compliance Tag" },
            new() { TypeName = "microsoft.securityandcompliance.deviceConditionalAccessPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Device Conditional Access Policy" },
            new() { TypeName = "microsoft.securityandcompliance.deviceConfigurationPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Device Configuration Policy" },
            new() { TypeName = "microsoft.securityandcompliance.dlpCompliancePolicy", Workload = "SecurityAndCompliance", FriendlyName = "DLP Compliance Policy" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertyAuthority", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Authority" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertyCategory", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Category" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertyCitation", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Citation" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertyDepartment", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Department" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertyReferenceId", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Reference ID" },
            new() { TypeName = "microsoft.securityandcompliance.filePlanPropertySubCategory", Workload = "SecurityAndCompliance", FriendlyName = "File Plan Property - Sub Category" },
            new() { TypeName = "microsoft.securityandcompliance.labelPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Label Policy" },
            new() { TypeName = "microsoft.securityandcompliance.protectionAlert", Workload = "SecurityAndCompliance", FriendlyName = "Protection Alert" },
            new() { TypeName = "microsoft.securityandcompliance.retentionCompliancePolicy", Workload = "SecurityAndCompliance", FriendlyName = "Retention Compliance Policy" },
            new() { TypeName = "microsoft.securityandcompliance.retentionComplianceRule", Workload = "SecurityAndCompliance", FriendlyName = "Retention Compliance Rule" },
            new() { TypeName = "microsoft.securityandcompliance.retentionEventType", Workload = "SecurityAndCompliance", FriendlyName = "Retention Event Type" },
            new() { TypeName = "microsoft.securityandcompliance.securityFilter", Workload = "SecurityAndCompliance", FriendlyName = "Security Filter" },
            new() { TypeName = "microsoft.securityandcompliance.supervisoryReviewPolicy", Workload = "SecurityAndCompliance", FriendlyName = "Supervisory Review Policy" },
            new() { TypeName = "microsoft.securityandcompliance.supervisoryReviewRule", Workload = "SecurityAndCompliance", FriendlyName = "Supervisory Review Rule" }
        ]
    };

    /// <summary>
    /// Reverse lookup from resource type to workload.
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeToWorkload;

    /// <summary>
    /// Lookup from resource type to friendly name.
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeToFriendlyName;

    static UtcmResourceTypeRegistry()
    {
        ResourceTypeToWorkload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ResourceTypeToFriendlyName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (workload, resourceTypes) in WorkloadMappings)
        {
            foreach (var resourceType in resourceTypes)
            {
                ResourceTypeToWorkload[resourceType.TypeName] = workload;
                ResourceTypeToFriendlyName[resourceType.TypeName] = resourceType.FriendlyName;
            }
        }
    }

    /// <summary>
    /// Gets all supported workload names.
    /// </summary>
    public static IReadOnlyList<string> GetWorkloads() => WorkloadMappings.Keys.ToList();

    /// <summary>
    /// Gets all resource type names for a workload.
    /// </summary>
    /// <param name="workload">The workload name (case-insensitive).</param>
    /// <returns>List of resource type names, or empty list if workload not found.</returns>
    public static IReadOnlyList<string> GetResourceTypes(string workload)
    {
        if (WorkloadMappings.TryGetValue(workload, out var types))
        {
            return types.Select(t => t.TypeName).ToList();
        }
        return [];
    }

    /// <summary>
    /// Gets full resource type information for a workload.
    /// </summary>
    /// <param name="workload">The workload name (case-insensitive).</param>
    /// <returns>List of resource types with metadata, or empty list if workload not found.</returns>
    public static IReadOnlyList<UtcmResourceType> GetResourceTypeInfo(string workload)
    {
        if (WorkloadMappings.TryGetValue(workload, out var types))
        {
            return types;
        }
        return [];
    }

    /// <summary>
    /// Gets the workload name for a resource type.
    /// </summary>
    /// <param name="resourceType">The UTCM resource type name.</param>
    /// <returns>Workload name, or null if not found.</returns>
    public static string? GetWorkloadForResourceType(string resourceType)
    {
        return ResourceTypeToWorkload.TryGetValue(resourceType, out var workload) ? workload : null;
    }

    /// <summary>
    /// Gets the friendly name for a resource type.
    /// </summary>
    /// <param name="resourceType">The UTCM resource type name.</param>
    /// <returns>Friendly name, or the resource type itself if not found.</returns>
    public static string GetFriendlyName(string resourceType)
    {
        return ResourceTypeToFriendlyName.TryGetValue(resourceType, out var name) ? name : resourceType;
    }

    /// <summary>
    /// Gets all resource types for multiple workloads.
    /// </summary>
    /// <param name="workloads">The workload names to include.</param>
    /// <returns>Combined list of resource type names.</returns>
    public static IReadOnlyList<string> GetResourceTypesForWorkloads(IEnumerable<string> workloads)
    {
        var result = new List<string>();
        foreach (var workload in workloads)
        {
            result.AddRange(GetResourceTypes(workload));
        }
        return result;
    }

    /// <summary>
    /// Gets all known resource types across all workloads.
    /// </summary>
    public static IReadOnlyList<UtcmResourceType> GetAllResourceTypes()
    {
        return WorkloadMappings.Values.SelectMany(x => x).ToList();
    }

    /// <summary>
    /// Checks if a workload name is valid.
    /// </summary>
    public static bool IsValidWorkload(string workload)
    {
        return WorkloadMappings.ContainsKey(workload);
    }

    /// <summary>
    /// Checks if a resource type is known.
    /// </summary>
    public static bool IsValidResourceType(string resourceType)
    {
        return ResourceTypeToWorkload.ContainsKey(resourceType);
    }
}
