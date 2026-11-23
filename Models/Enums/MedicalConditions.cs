namespace NutriTrackAI.Models.Enums;

[Flags]
public enum MedicalConditions
{
    None = 0,
    Diabetes = 1,
    UsesInsulinOrGlucoseLowering = 2,
    Gastritis = 4,
    Hypertension = 8,
    HeartDisease = 16,
    Gout = 32,
    Hypothyroidism = 64,
    KidneyDisease = 128,
    EatingDisorderHistory = 256,
    PregnantOrBreastfeeding = 512,
    OtherCondition = 1024
}
