# Tiny State Machine

**Tiny State Machie** is a lightweight [Finite State Machine](http://t.co/)
library for the .NET framework written in C#. It's so lightweight
that it consists of a single source file. There are no assemlbies to reference
and no binaries to wrangle.

This fork has a few major changes:
* States are stored in a separate object from the state machine itself. This allows for one machine to be shared by many objects.
* Transitions may take a single argument. If a transition is declared as taking an argument, but triggred without one, and exception is thrown.
* Creating the fsm and running it have been split out into separate classes to remove much of the run time checking and state. The fluent interface 
  should be harder to get wrong.
* Some error conditions have been moved to Conditional("Debug") when then are not expected to be encountered.
* Only enums are supported for state and triggers

# Example
A good example is a Finite State Machine (FSM) for a door. To keep
things as simple as possible, let's assume that the door can be in one
of two states: *Closed* and *Opened*. We'll also need triggers (or events)
that will cause the door to change state. There are two of those as well:
*Close* and *Open*.

Here's the [state transition table](https://en.wikipedia.org/wiki/State_transition_table) for this FSM

| Current state | Trigger | Next state |
| --- | --- | --- |
| Closed | Open  | Opened |
| Opened | Close | Closed |

And here's how this table is represented in Tiny State Machine:

~~~c#
public enum DoorState {
    Closed,
    Open,
}

public enum DoorEvents {
    Open,
    Close,
}

public class Door : TinyStateMachine<DoorState, DoorEvents>.IStorage {
    public TinyStateMachine<DoorState, DoorEvents>.Storage Memory { get; set; }
    public bool WasSlammed { get; set; }
}

public void WorkTheDoor() {
    // Declare the FSM and specify the starting state.
    var doorFsmCompiler = TinyStateMachine<DoorState, DoorEvents>.Create<Door>(DoorState.Closed);

    // Now configure the state transition table.
    doorFsmCompiler.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
            .Tr<bool>(DoorState.Open, DoorEvents.Close, DoorState.Closed)
            .On((d, slammed) => d.WasSlammed = slammed);

    var doorFsm = doorFsmCompiler.Compile();

    var door = new Door() { Memory = doorFsm.CreateMemory() };

    // As specified in the constructor, the door starts closed.
    Debug.Assert(door.Memory.IsInState(DoorState.Closed));

    // Let's trigger a transition
    doorFsm.Fire(DoorEvents.Open, door);

    // Door is now open.
    Debug.Assert(door.Memory.IsInState(DoorState.Open));

    // create as many doors as needed
    var otherDoor = new Door() { Memory = doorFsm.CreateMemory() };

    // The state machine is shared, but the state is not
    Debug.Assert(otherDoor.Memory.IsInState(DoorState.Closed));

    // According to the transition table, closing a door requires
    // a bool argument. The following will throw an exception
    bool exceptionWasThrown = false;
    try {
        // Let's trigger the other transition
        doorFsm.Fire(DoorEvents.Close, door);
    }
    catch {
        exceptionWasThrown = true;
    }
    Debug.Assert(exceptionWasThrown == true);

    // Door is still open.
    Debug.Assert(door.Memory.IsInState(DoorState.Open));

    // Slam it this time
    doorFsm.Fire(DoorEvents.Close, door, true);

    // Door is now closed.
    Debug.Assert(door.Memory.IsInState(DoorState.Closed));

    // Door is was slammed.
    Debug.Assert(door.WasSlammed);

    // still closed
    Debug.Assert(otherDoor.Memory.IsInState(DoorState.Closed));

    // According to the transition table, a closed door
    // cannot be closed. The following will throw an exception
    exceptionWasThrown = false;
    try {
        doorFsm.Fire(DoorEvents.Close, door);
    }
    catch {
        exceptionWasThrown = true;
    }
    Debug.Assert(exceptionWasThrown == true);
}
~~~


# Requirements
Tiny State Machine runs on:

*   The .NET Framework 4 and above.
*   Unity3D 4.6 and above.

It might work with other frameworks and/or versions, but these are
the ones that I have tested.

# Installation
Download the file [TinyStateMachine.cs](https://github.com/MhmmdAb/TinyStateMachine/blob/master/TinyStateMachine.cs)
and add it to your project.

# License
Tiny State Machine is released under the [MIT license](https://github.com/MhmmdAb/TinyStateMachine/blob/master/LICENSE).
Crediting the [author](http://m16h.com) is highly appreciated but not at all
required.

# Credits
  * The whole *tiny* philosophy of small, single file libraries was inpspired by
    [TinyMessenger](https://github.com/grumpydev/TinyMessenger).
  * Some of the terminology and concepts were borrowed from
    [Stateless](https://github.com/dotnet-state-machine/stateless), an
excellent and more elaborate FSM implementation for .NET.
  * The state transition table concept was inpsired by
    [Boost's Meta State Machine](http://www.boost.org/doc/libs/release/libs/msm/).
