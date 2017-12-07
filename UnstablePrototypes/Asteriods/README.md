## milestones

### naive networked (no prediction, no interpolation) poc
  - deliverables
    - [ ] multiplayer packet - integrate with the "new" raw udp socket library, bit/byte writer
    - [ ] guideline document - seperation of client / server and shared code.
    - [ ] ~1k objects synchronized - client move + draw / server handling collisions.
    - [ ] state and command synchronization (e.g. should be roughly playable on lan)

### snapshot interpolation
  - deliverables
    - [ ] multiplayer packet - reliability layer, snapshot code, time/frame synchronization
    - [ ] guideline document - snapshot interpolations
    - [ ] 5k objects synchronized - client move + draw / server handling collisions.
    - [ ] state and command synchronization using snapshot interpolation. (e.g playable with 100ms and 2% pkt loss)

### snapshot compression
  - deliverables
    - [ ] multiplayer packet - delta compression, area of interest / prioritization of packets
    - [ ] guideline document - compression
    - [ ] 15k+ objects synchronized - client move + draw / server handling collisions.
    - [ ] state and command synchronization using snapshot interpolation with delta compression. (e.g playable with 100ms and 2% pkt loss)

### todo
- current milestone: naive networked

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
  [ ] make it possible to certain systems with a different framerate

- game code
  - [ ] make collision detection more scalable, look at boids demo!
  - [ ] move away from game objects!
    - [ ] remove sprite renderer
  - [ ] add a follow cam
  - [ ] decide on map size and maybe even make it wrap.
  - [ ] add health component to all objects?
  - [ ] make sure we can spawn 5k objects on the map now within reasonoble time and run them without drop in framerate.
  - [ ] divide into server/server and common code.

- console system
  - [ ] move over from monobehavior -> systems?

- networking
  - [ ] add sockets
  - [ ] byte writer / reader