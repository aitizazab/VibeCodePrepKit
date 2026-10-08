// =====================================================================
// LAST TRAIN HOME: the lost property office, a point-and-click scene. PLACEHOLDER PROSE.
//
// How it works: "office" opens the hotspot scene (#investigate:office) and stops (-> DONE).
// The player clicks things; each hotspot runs its knot below (the hotspots, their shapes and
// which item does what are in Templates/Story/Data/Hotspots/Office.asset). When a knot ends
// with -> DONE the player is back in the office. #leave closes the scene and the story goes on.
// =====================================================================

VAR found_key = false
VAR drawer_open = false
VAR found_photo = false

=== office ===
#scene:office black 1
#ambience:amb_hum 1
The office smells of dust and wet wool. A desk, a coat stand, shelves of forgotten things, and a door back to the waiting room.
{ visited_ever("office") && playthrough_count() > 1:
    The light switch is already on. You don't remember turning it on. You don't remember not turning it on. #thought
}
Somewhere in here is a brown ticket with a name on the back. #investigate:office
-> DONE

=== office_coat ===
{ found_key:
    The coat again. It smells of pipe smoke and peppermints. Bram's, surely.
- else:
    A heavy grey coat on the stand, the kind with pockets deep enough to lose a cat in. #sfx:page
    In the left pocket: a small brass key on a loop of string. #sfx:item
    ~ give(brass_key)
    ~ found_key = true
    A key near the desk. Bram really is a creature of habit. #thought
}
-> DONE

=== office_drawer ===
{ drawer_open:
    The drawer is empty now, apart from a pencil stub and three paper clips holding hands.
- else:
    The top drawer of the desk is locked. The keyhole is small and brass-coloured. #sfx:click
}
-> DONE

// Using the brass key on the drawer (Office.asset: Item Uses).
=== office_drawer_key ===
{ drawer_open:
    It's already open.
    -> DONE
}
The little key turns with a sound like a knuckle cracking. #sfx:unlock
~ drawer_open = true
#cg:cg_ticket fade 0.8
Inside, on top of a stack of unpaid bills: a brown card ticket. WEXLEY, SINGLE. #sfx:item
~ give(ticket)
#cg:off fade 0.6
That's it. That's hers. #thought
-> DONE

=== office_desk ===
Bram's desk. A logbook lies open beside a cold cup of tea.
<i>14 Oct. Lady in yellow coat asking after the 23:52 again. Told her it's delayed. It's always delayed.</i>
{ playthrough_count() > 1:
    The entry below is in handwriting you know very well. It's yours. It says: <i>Don't forget the drawer.</i> #glitch:0.3 0.5
}
-> DONE

=== office_shelves ===
{ found_photo:
    Umbrellas, gloves, a single roller skate, a jar of buttons. Nothing that's anyone's any more.
- else:
    Shelves and shelves of lost things, each with a paper tag. UMBRELLA, BLACK. GLOVE, LEFT. HOPE, SMALL. #sfx:page
    One tag has no object beside it, just an old photograph propped against the wall. You take it without quite deciding to. #sfx:item
    ~ give(photograph)
    ~ found_photo = true
}
-> DONE

=== office_board ===
A notice board. Most of the notices are about a jumble sale in 1987. #sfx:page
The timetable in the middle has one train on it. <b>23:52 WEXLEY</b>. Someone has underlined it so many times the paper has torn.
-> DONE

=== office_window ===
#zoom:1.25 0.8 70% 30%
Rain on the glass, and the platform beyond it under its one lamp.
{ playthrough_count() > 1:
    In the reflection, the waiting room behind you is empty. Then it isn't. #glitch:0.4 0.4
- else:
    You can just see the yellow of her coat through the waiting room door, waiting.
}
#zoom:1 0.8
-> DONE

=== office_door ===
{ has(ticket):
    You have what you came for. #leave
    -> waiting_room_return
- else:
    You can't go back to her empty-handed. Not yet. #thought
    -> DONE
}

// Any item used where it does nothing (Office.asset: Fallback Item Knot).
=== office_no_use ===
{~That doesn't help here.|You try it. Nothing happens. The rain carries on regardless.|No. Not that.}
-> DONE
