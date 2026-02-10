# RAG Traceability Audit Log

This log shows the **Actual Retrieved Contexts** for each question.
*Aegis vs. LangChain (with overlap=200)*

### Strategy: Aegis (Geometric)
**Question**: What is the threshold for blacklisting a fragment in the TraceMonkey implementation?

> **Context 1** (Truncated):
> Trace-based Just-in-Time Type Specialization for Dynamic Languages Andreas Gal∗+, Brendan Eich∗, Mike Shaver∗, David Anderson∗, David Mandelin∗, Mohammad R. Haghighat$, Blake Kaplan∗, Graydon Hoare∗, Boris Zbarsky∗, Jason Orendorff∗, Jesse Ruderman∗, Edwin Smith#, Rick Reitmaier#, Michael Bebenita+, Mason Chang+#, Michael Franz+ Mozilla Corporation∗ {gal,brendan,shaver,danderson,dmandelin,mr...

---

### Strategy: Aegis (Geometric)
**Question**: Describe the potential performance issue with small loops that get blacklisted.

> **Context 1** (Truncated):
> Trace-based Just-in-Time Type Specialization for Dynamic Languages Andreas Gal∗+, Brendan Eich∗, Mike Shaver∗, David Anderson∗, David Mandelin∗, Mohammad R. Haghighat$, Blake Kaplan∗, Graydon Hoare∗, Boris Zbarsky∗, Jason Orendorff∗, Jesse Ruderman∗, Edwin Smith#, Rick Reitmaier#, Michael Bebenita+, Mason Chang+#, Michael Franz+ Mozilla Corporation∗ {gal,brendan,shaver,danderson,dmandelin,mr...

---

### Strategy: LangChain (Text)
**Question**: What is the threshold for blacklisting a fragment in the TraceMonkey implementation?

> **Context 1** (Truncated):
> Monkey enters recording mode on line 4. In recording mode, TraceMonkey records the code along the trace in a low-level compiler intermediate representation we call LIR. The LIR trace encodes all the operations performed and the types of all operands. The LIR trace also encodes guards, which are checks that verify that the control flow and types are identical to those observed during trace re...

> **Context 2** (Truncated):
> t trace, using the loop header of the root trace as the target to reach. Our implementation does not extend at all side exits. It extends only if the side exit is for a control-flow branch, and only if the side exit does not leave the loop. In particular we do not want to extend a trace tree along a path that leads to an outer loop, because we want to cover such paths in an outer tree through...

---

### Strategy: LangChain (Text)
**Question**: Describe the potential performance issue with small loops that get blacklisted.

> **Context 1** (Truncated):
> it will not try to record a trace starting at that point until it is passed a few more times (32 in our implementation). This backoff counter gives temporary conditions that prevent tracing a chance to end. For example, a loop may behave differently during startup than during its steady-state execution. After a given number of failures (2 in our implementation), the VM marks the fragment as ...

> **Context 2** (Truncated):
> unction object used to compute the replacement text. Our implementation currently does not trace functions called as replace functions. The run time of string-unpack-code is dominated by such a replace call. • Two programs trace well, but have a long compilation time. access-nbody forms a large number of traces (81). crypto-md5 forms one very long trace. We expect to improve performance on ...

---

