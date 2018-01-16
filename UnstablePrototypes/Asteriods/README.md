# README

## milestones

### naive networked (no prediction, no interpolation) poc

- deliverables
  - [ ] multiplayer packet - integrate with the "new" raw udp socket library, bit/byte writer
  - [ ] guideline document - seperation of client / server and shared code.
  - [ ] ~1k objects synchronized
    - client sends input to server, and draws objects
    - server handles collisions and inputs from clients + moves the entities.
  - [ ] state and command synchronization (e.g. should be roughly playable on lan)

  - test for completion
    - you should be able to start a server through a player/editor and connect multiple clients (also using player/editor and through lan).
    - you should be able to spawn up to ~ 1k objects on the server (asteroids) that will be synchronized on each connected client.

  - deliverables
    - [ ] multiplayer packet - reliability layer, snapshot code, time/frame synchronization
    - [ ] guideline document - snapshot interpolations, server / client seperation handling and startup.
    - [ ] 5k objects synchronized - client move + draw / server handling collisions.
    - [ ] state and command synchronization using snapshot interpolation. (e.g playable with 100ms and 2% pkt loss)

### snapshot compression

- deliverables
  - [ ] multiplayer packet - delta compression, area of interest / prioritization of packets
  - [ ] guideline document - compression
  - [ ] 15k+ objects synchronized - client move + draw / server handling collisions.
  - [ ] state and command synchronization using snapshot interpolation with delta compression. (e.g playable with 100ms and 2% pkt loss)

## todo

### current milestone: naive networked

- misc
  - [x] back of the envelope calculations for whats technically doable on a 1GB line.

```
    state = data * users
    total_transmission = state * users * hz

    t = d * u^2 * h
    u = sqrt(t/(d*h))

    no compression sending 128 bits / user on a 10hz server saturates the line after ~883 users
    no compression sending 128 bits / user on a 60hz server saturates the line after ~360 users

    if we say we dont go over the 1500bit pkt size
    u = (h * s)/t
    then we can send 66k 1500bit pkts running @ 10hz
    then we can send 11k 1500bit pkts running @ 60hz
```

- ecs
  - [ ] make it possible to certain systems with a different framerate

- editor
  - [ ] think of how seperation should be done for client / server code
    - questions:
      - should we go the sample game path? e.g. PostProcess and SceneStripping
      - how to handle spawning of client and server instances?

- game code
  - [ ] make collision detection more scalable, look at boids demo!
    - move collision detection to server and use only circle for now. (michalb)
  - [x] move away from game objects!
    - [x] remove sprite renderer (timj)
  - [ ] add a follow cam
  - [ ] decide on map size and maybe even make it wrap.
  - [ ] add health component to all objects?
  - [ ] make sure we can spawn 5k objects on the map now within reasonoble time and run them without drop in framerate.

  - [x] divide into server/server and common code. (michalb)
    - ~seperate into proper folders~
    - ~divide client / server code from the systems~
    - ~move server code into pure ecs.~
    - ~start seperate worlds for client and server~
    - ~introduce state and cmd structures~
    - ~fix respawning~
    - ~find out why we are getting an exception in Client.SpawnSystem:110~
  - [x] add multiple worlds for faster prototyping for client / server

  - [ ] misc fixes that is still missing for multiple worlds
    - add attributes for client / server code and remove the namespace hack in GameMain
    - GameSettings and archetypes need to have their respective owners set properly. e.g. remvoe hack
  - handle respawns
  - start looking at proper snapshotting

- debug tools
  - console system
    - [ ] move over from monobehavior -> systems?

- networking
  - [x] add sockets
  - [ ] serialize all data using packets
  - [ ] byte writer / reader
    - write tests for bit reader & writer
    - write tests for byte reader & writer
  - ~move the packet reading into a circular buffer type of class.~
  - move network layer to c#?
  - fix the fact that we actually dont handle disconnects at all in the llapi now.


#### notes

- how do we want to make the serialization of network messages? need to think this through as it is gonna be important for debugging later.
  - mark type of messages, generate code/il/other, 
- seems to be a bit much duplication between NetworkServer & NetworkClient, Dont know if NetworkTransport/Interface would be better. 
