**Parachute system**

Thanks to @k1lly0u to point me in the right direction while i was strungling

**Description:**
A new way to smoothly glide down with a parachute, fully customisable (speed of parachute, fall speed, etc)
When navigate to the sides, the parachute will be on the side, to give a bit more reality to it.

**Permissions:**
Either be an admin, or have oxide's permission: `parachute.allowed` 

**Configuration:**
You may change the cooldown options freely
The Parachute option might be tricky to use, so be aware before editing it.

```
{
  "Cooldown Options": {
    "Use Cooldown (true/false)": true,
    "Timer (number)": 10.0
  },
  "Parachute Options (for advanced users only)": {
    "Up force, to counter the gravity fall": 7.0,
    "Max drop speed, before the plugin starts to give an extra break from gravity fall": -10.0,
    "Forward acceleration strength": 6.0,
    "Backward acceleration strength": 4.0,
    "Rotation acceleration strength": 0.4,
    "Forward resistance (will slow down constantly the parachute)": 0.3,
    "Rotation resistance (will reduce the rotation if the player stops pressing rotation)": 0.5,
    "Auto release parachute height": 1.5,
    "Auto release parachute proximity": 0.5,
    "Angular modifier (how much your are on the side depending on your rotation speed)": 50.0
  }
}
```


**For Developers**
*As i don't have much time to code, my plugins are always welcomed to be improved by others, and may be taken over if you have updates/improvements to do*

I kept from the Chute plugin
`void ExternalAddPlayerChute(BasePlayer player)`

`TryDeployParachuteOnPlayer(BasePlayer player)`
It will try to add a parachute to the player on his position, but will check if the player is already mounting / on the ground / cooldown blocked

`DeployParachuteOnPlayer(BasePlayer player)`
 It will add a parachute to the player on his position and will ignore all checks

`DeployParachuteOnPosition(BasePlayer player, Vector3 position)`
 It will teleport the player to the designated position and add a parachute to the player

`DeployParachuteOnPositionAndRotation(BasePlayer player, Vector3 position, Vector3 rotation)`
It will teleport the player to the designated position, set his rotation angle and add a parachute to the player