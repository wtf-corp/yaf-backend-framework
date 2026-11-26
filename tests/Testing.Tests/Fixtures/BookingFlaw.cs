namespace Testing.Tests.Fixtures;

// The bug a Booking's RestoreCore simulates. It is part of the booking's state, so it survives the round trip itself.
internal enum BookingFlaw
{
    None,
    ForgetsTitle,
    ForgetsDataSubject,
    LosesLastLine,
    ForgetsLineQuantity,
}
