// Integration example for an independent mod; NOT installed or
// compiled as a gameplay plugin. No co-op assembly reference is needed.
using System;
using UnityEngine.InputSystem;
using ZombieGhost.Rebind;

public sealed class ExampleModBindings : IDisposable
{
    private readonly RebindProvider provider;
    public RebindButton CameraDrag { get; }

    public ExampleModBindings()
    {
        // Replace with your mod's permanent GUID/name.
        provider = RebindApi.RegisterProvider("example.mod.controls", "Example Mod Controls");
        CameraDrag = provider.RegisterButton("camera.drag", "Hold to rotate camera",
            "<Mouse>/middleButton", "Camera");
    }

    // Only needed if your mod introduces its OWN movement InputAction.
    // The existing game's Persistent/Move action is already automatically listed.
    public void RegisterModOwnedMovement(InputAction movement)
    {
        provider.RegisterAction("movement", movement, "Movement", "Movement");
    }

    // Replace raw middleButton.isPressed / wasPressedThisFrame / wasReleasedThisFrame
    // reads with CameraDrag.IsPressed / WasPressedThisFrame / WasReleasedThisFrame.
    // Keep the existing movement/camera implementation in the controls mod itself.
    public void Dispose() => provider.Dispose();
}

