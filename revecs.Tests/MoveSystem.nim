import macros;

macro system(n: untyped) = discard 
macro queryReq() = discard 

type
  Position = object
    x*: int

proc cheese(): Position = discard 

system MoveSystem:
  for ent in queryReq(
    pos: var Position, 
    vel: Velocity
  ):
    pos += vel * resourceReq(GameTime).Delta