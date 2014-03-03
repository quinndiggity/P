event Ping:id assert 1;
event Pong assert 1;
event Success;

main machine PING {
    var pongId: id;

    start state Ping_Init {
        entry {
	    new M();
  	    pongId = new PONG();
	    raise (Success);   	   
        }
        on Success goto Ping_SendPing;
    }

    state Ping_SendPing {
        entry {
	    invoke M(Ping);
	    send (pongId, Ping, this);
	    raise (Success);
	}
        on Success goto Ping_WaitPong;
     }

     state Ping_WaitPong {
        on Pong goto Ping_SendPing;
     }

    state Done { }
}

machine PONG {
    start state Pong_WaitPing {
        entry { }
        on Ping goto Pong_SendPong;
    }

    state Pong_SendPong {
	entry {
	     invoke M(Pong);
	     send ((id) payload, Pong);
	     raise (Success);		 	  
	}
        on Success goto Pong_WaitPing;
    }
}

monitor M {
    start state ExpectPing {
        on Ping goto ExpectPong;
    }

    state ExpectPong {
        on Pong goto ExpectPing;
    }
}