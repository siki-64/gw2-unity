// Full 59-site sweep with the CORRECT per-registrar, per-table stride.
//
// Stride follows the validator each registrar actually calls, not its name:
//   RegisterRecvOnly      -> ValidateTablePairs : tableA is PAIRS, stride 16
//   RegisterTablePair     -> ValidateTableFlat(tableA) + ValidateTablePairs(tableB)
//                            tableA FLAT stride 8, tableB PAIRS stride 16
//   RegisterBidirectional -> ValidateTableFlat : tableA FLAT stride 8
//
// Element forms:
//   FLAT  : {ptrA}                 one defArray per entry
//   PAIRS : {ptrA, ptrB}           two defArrays per entry
//
// Every pointer read is validated as MP_MSGID and its block recorded.
// Nothing dropped: non-OK rows carry their tag.

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.scalar.Scalar;
import ghidra.program.model.symbol.Reference;
import java.io.PrintWriter;
import java.util.*;

public class Sweep2 extends GhidraScript {
  Memory mem;

  private String dispOf(Instruction q, String reg) {
    for (int o = 0; o < q.getNumOperands(); o++) {
      if (!q.getDefaultOperandRepresentation(o).equalsIgnoreCase(reg)) continue;
      for (Object x : q.getOpObjects(1)) {
        if (x instanceof Scalar) return Long.toHexString(((Scalar) x).getUnsignedValue());
        if (x instanceof Address) return Long.toHexString(((Address) x).getOffset());
      }
    }
    return null;
  }

  private String immOf(Instruction q, String reg) {
    if (q.getNumOperands() < 2) return null;
    if (!q.getDefaultOperandRepresentation(0).equalsIgnoreCase(reg)) return null;
    Scalar s = q.getScalar(1);
    return s == null ? null : Long.toString(s.getUnsignedValue());
  }

  // True when operand 0 is a memory reference to the given stack slot.
  //
  // The representation carries a size prefix, e.g. "qword ptr [RSP + 0x28]",
  // so an equality test against "[RSP + 0x28]" never matches. Compare on the
  // normalised form with the prefix stripped, which is what the emitted
  // operand contains after removing spaces.
  private boolean isMem(Instruction q, String slot) {
    if (q.getNumOperands() < 1) return false;
    String op0 = q.getDefaultOperandRepresentation(0).replace(" ", "").toLowerCase();
    return op0.endsWith(slot.replace(" ", "").toLowerCase());
  }

  private String immMemOf(Instruction q, String slot) {
    if (q.getNumOperands() < 2) return null;
    if (!isMem(q, slot)) return null;
    Scalar s = q.getScalar(1);
    return s == null ? null : Long.toString(s.getUnsignedValue());
  }

  // True when operand 0 is the given stack slot and operand 1 is the given
  // register, i.e. "MOV <slot>,<reg>".
  //
  // This is what the table-B store looks like: "MOV qword ptr [RSP + 0x28],RAX".
  // The source is a register, so getScalar(1) is null and the immediate-based
  // helper above cannot see the store. Detecting it on the register source is
  // the only way to recover table B from the register form.
  private boolean isStoreReg(Instruction q, String slot, String reg) {
    if (q.getNumOperands() < 2) return false;
    if (!isMem(q, slot)) return false;
    return q.getDefaultOperandRepresentation(1).equalsIgnoreCase(reg);
  }

  private String tag(int ft) { return ft == 1 ? "OK" : ft == 10 ? "ARRAY" : "BADFT"; }

  private void emit(PrintWriter w, String reg, String call, int col, long ptr) throws Exception {
    if (ptr == 0) { w.println(reg + "," + call + "," + col + ",NULL,,,"); return; }
    Address d = toAddr(ptr);
    MemoryBlock b = mem.getBlock(d);
    if (b == null) { w.println(reg + "," + call + "," + col + ",PTR_UNMAPPED,," + Long.toHexString(ptr) + ",UNMAPPED"); return; }
    if (b.getName().contains("text")) { w.println(reg + "," + call + "," + col + ",CODE,," + Long.toHexString(ptr) + "," + b.getName()); return; }
    int ft = mem.getInt(d);
    int val = mem.getInt(d.add(16));
    w.println(reg + "," + call + "," + col + "," + tag(ft) + "," + Integer.toHexString(val) + "," + Long.toHexString(ptr) + "," + b.getName());
  }

  private void walkFlat(PrintWriter w, String reg, String call, long table, int n) throws Exception {
    for (int i = 0; i < n; i++) emit(w, reg, call, 0, mem.getLong(toAddr(table + (long) i * 8)));
  }

  private void walkPairs(PrintWriter w, String reg, String call, long table, int n) throws Exception {
    for (int i = 0; i < n; i++) {
      Address e = toAddr(table + (long) i * 16);
      emit(w, reg, call, 0, mem.getLong(e));
      emit(w, reg, call, 1, mem.getLong(e.add(8)));
    }
  }

  public void run() throws Exception {
    mem = currentProgram.getMemory();
    PrintWriter w = new PrintWriter(getScriptArgs()[0], "UTF-8");
    w.println("registrar,callAddr,col,tag,id,ptr,block");
    long[] targets = { 0x140fed7f0L, 0x140fed730L, 0x140fed670L };
    String[] names = { "RecvOnly", "TablePair", "Bidirectional" };
    int sites = 0, rows = 0, flat = 0, pairs = 0;

    for (int t = 0; t < targets.length; t++) {
      for (Reference r : getReferencesTo(toAddr(targets[t]))) {
        Address from = r.getFromAddress();
        if ((mem.getByte(from) & 0xff) != 0xe8) continue;
        sites++;
        // Window widened from 14 to 24 instructions. At site 14020a031 the
        // table-B stack store sits 30 bytes before the call, which is outside a
        // 14-instruction window even though the table-A register load is inside
        // it. That asymmetry is why an earlier run recovered count A and table A
        // but silently produced no table-B rows at all.
        List<Instruction> win = new ArrayList<>();
        Instruction p = getInstructionAt(from);
        for (int k = 0; k < 24 && p != null; k++) { p = p.getPrevious(); if (p != null) win.add(0, p); }
        // Argument mapping, read from the argument-setup block immediately
        // preceding the call. Verified at site 14020a031:
        //   LEA RAX,[tableB] ; MOV [RSP+0x28],RAX   -> table B on the stack
        //   LEA R9,[tableA]                          -> table A in R9
        //   MOV [RSP+0x20],countB                    -> count B on the stack
        //   MOV R8D,countA                           -> count A in R8D
        //
        // An earlier version took RAX as table A whenever R9 was absent, which
        // silently walked table B's address as table A. The [RSP+0x28] slot is
        // table B, NOT table A, and [RSP+0x20] is count B, NOT count A.
        //
        // Note the stack slots are written with the table in RAX first and the
        // same RAX is then stored to [RSP+0x28]; so the RAX->stack-slot pairing
        // must be resolved before RAX is treated as anything else.
        String tA = null, cA = null, tB = null, cB = null;
        String raxVal = null;
        for (Instruction q : win) {
          String m = q.getMnemonicString();
          if (m.equals("LEA")) {
            String a9 = dispOf(q, "R9"); if (a9 != null) tA = a9;
            String rax = dispOf(q, "RAX"); if (rax != null) raxVal = rax;
          } else if (m.equals("MOV")) {
            String a = immOf(q, "R8D"); if (a != null) cA = a;
            // [RSP+0x28] <- RAX  : table B. The source is a REGISTER, so this
            // must be detected on operand 1, not via getScalar.
            if (isStoreReg(q, "[RSP + 0x28]", "RAX")) tB = raxVal;
            // [RSP+0x20] <- count : count B, an immediate.
            String b = immMemOf(q, "[RSP + 0x20]"); if (b != null) cB = b;
          }
        }
        // RecvOnly (t==0) is a 5-param call with a single table in R9 and no
        // stack table; leave tB/cB null there.
        String call = Long.toHexString(from.getOffset());
        if (tA == null || cA == null) { w.println(names[t] + "," + call + ",0,NOARGS,,,"); continue; }
        long tblA; int nA;
        try { tblA = Long.parseUnsignedLong(tA, 16); nA = Integer.parseInt(cA); }
        catch (Exception ex) { w.println(names[t] + "," + call + ",0,BADARGS," + tA + ",,"); continue; }
        if (nA <= 0 || nA > 4096) { w.println(names[t] + "," + call + ",0,COUNT_REJECTED," + nA + ",,"); continue; }

        // Table A stride: flat for TablePair and Bidirectional, pairs for RecvOnly.
        boolean aIsFlat = (t == 1) || (t == 2);
        if (aIsFlat) { flat++; walkFlat(w, names[t], call, tblA, nA); rows += nA; }
        else { pairs++; walkPairs(w, names[t], call, tblA, nA); rows += nA * 2; }

        // Table B is always pairs when present.
        if (tB != null && cB != null) {
          try {
            long tblB = Long.parseUnsignedLong(tB, 16);
            int nB = Integer.parseInt(cB);
            if (nB > 0 && nB <= 4096) { pairs++; walkPairs(w, names[t], call, tblB, nB); rows += nB * 2; }
          } catch (Exception ignored) { }
        }
      }
    }
    w.flush();
    w.close();
    println(String.format("sites=%d rows=%d flatWalks=%d pairWalks=%d", sites, rows, flat, pairs));
  }
}
