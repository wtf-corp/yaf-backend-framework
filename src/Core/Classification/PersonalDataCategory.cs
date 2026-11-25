namespace YAF.Core.Classification;

/// <summary>
/// What kind of personal data a member holds. Given to <see cref="PersonalDataAttribute"/> and <see cref="SensitivePersonalDataAttribute"/>;
/// listed in the data inventory.
/// </summary>
public enum PersonalDataCategory
{
    /// <summary>Names and identifiers of a person.</summary>
    Identity,

    /// <summary>Email addresses, phone numbers and postal addresses.</summary>
    Contact,

    /// <summary>Where a person is or was.</summary>
    Location,

    /// <summary>Financial account and payment data.</summary>
    Financial,

    /// <summary>Data about a person's health.</summary>
    Health,

    /// <summary>Biometric data.</summary>
    Biometric,

    /// <summary>What a person does: usage, preferences, online activity.</summary>
    Behavioural,

    /// <summary>A person's credentials.</summary>
    Credentials,

    /// <summary>Personal data that fits no other category.</summary>
    Other,
}
