namespace GraniteEdgeAI.Features.Onboarding
{
    public enum OnboardingStage
    {
        // The user imports or downloads a model.
        ImportModel = 1,

        // The application inspects the selected model.
        InspectModel = 2,

        // The application checks whether the model fits the hardware.
        CheckHardwareFit = 3,

        // The user chooses and validates an optimisation configuration.
        ConfigureModel = 4,

        // The model is ready and the user can enter Chat.
        ReadyToChat = 5
    }
}
