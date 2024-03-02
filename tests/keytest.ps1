while ($true) {
    $key = $host.ui.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    # Check for specific exit condition if necessary, for example:
    Write-Host "Character: " $key.Character " ControlKeyState: " $key.ControlKeyState " VirtualKeyCode: " $key.VirtualKeyCode
}