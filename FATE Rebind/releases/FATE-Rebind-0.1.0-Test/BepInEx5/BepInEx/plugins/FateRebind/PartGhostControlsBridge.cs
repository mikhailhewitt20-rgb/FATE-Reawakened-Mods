// Integration example for the future separated controls mod; NOT installed or
// compiled as a gameplay plugin. No co-op assembly reference is needed.
using System;
using UnityEngine.InputSystem;
using ZombieGhost.Rebind;

public sealed class PartGhostControlsBindings : IDisposable
{
    private readonly RebindProvider provider;
    public RebindButton CameraDrag { get; }

    public PartGhostControlsBindings()
    {
        // Replace with the separated controls mod's final GUID/name when known.
        provider = RebindApi.RegisterProvider("partghost.controls.example", "PartGhost Controls");
        CameraDrag = provider.RegisterButton("camera.drag", "Hold to rotate camera",
            "<Mouse>/middleButton", "Camera");
    }

    // Only needed if the separated mod introduces its OWN movement InputAction.
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
