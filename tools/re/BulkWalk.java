// Bulk walk of a MsgChannel static registration table.
//
// Args: <tableAddrHex> <entryCount> <outputCsvPath>
//
// CORRECTED LAYOUT (verified against raw bytes at 142167030 and by peeking the
// pointed-to descriptors):
//
//   table element, 16 bytes: { u64 defArrayA, u64 defArrayB }
//       BOTH columns are MsgPackFieldDef chain pointers, not a handler.
//       An earlier version of this script labelled column B "handlerFn" and
//       emitted a defArray pointer into the handler column. The hand-verified
//       cross-check caught it: the emitted "handler" 1425b6420 turned out to be
//       a valid descriptor whose fieldType is 1 and whose id is 0x1e, i.e. it
//       is the *next* schema pointer, not a function address.
//
//   descriptor, 40 bytes, stride 0x28 (layout from MsgPack_ComputeMaxSize at
//   140fe98b0, which walks the chain with ADD RDI,0x28):
//       +0x00 u32 fieldType   (1 == MP_MSGID, 0x0a == MP_ARRAY)
//       +0x10 u32 value       the message id when fieldType == MP_MSGID
//       +0x20 u64 nextDef     continuation of the chain, 0 == end
//
// Emits one CSV row per element with BOTH schemas resolved, tagged OK / ARRAY /
// BADFT / ZERO per column. Nothing is dropped silently.
//
// Read-only: uses getLong/getInt/getByte only, never writes to the program.

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import java.io.PrintWriter;

public class BulkWalk extends GhidraScript {

    private String tag(int ft) {
        return (ft == 1) ? "OK" : (ft == 10) ? "ARRAY" : "BADFT";
    }

    public void run() throws Exception {
        String[] a = getScriptArgs();
        long tableAddr = Long.parseUnsignedLong(a[0], 16);
        int count = Integer.parseInt(a[1]);
        String outPath = a[2];
        Memory mem = currentProgram.getMemory();
        PrintWriter w = new PrintWriter(outPath, "UTF-8");

        w.println("idx,colA_tag,colA_id,colA_ptr,colB_tag,colB_id,colB_ptr");

        int okA = 0, okB = 0, bad = 0;
        for (int i = 0; i < count; i++) {
            Address e = toAddr(tableAddr + (long) i * 16);
            long pA = mem.getLong(e);
            long pB = mem.getLong(e.add(8));

            String tagA = "ZERO", idA = "", ptrA = "";
            String tagB = "ZERO", idB = "", ptrB = "";

            if (pA != 0) {
                Address dA = toAddr(pA);
                int ftA = mem.getInt(dA);
                int vA = mem.getInt(dA.add(16));
                tagA = tag(ftA);
                idA = Integer.toHexString(vA);
                ptrA = Long.toHexString(pA);
                if (ftA == 1) okA++; else bad++;
            }
            if (pB != 0) {
                Address dB = toAddr(pB);
                int ftB = mem.getInt(dB);
                int vB = mem.getInt(dB.add(16));
                tagB = tag(ftB);
                idB = Integer.toHexString(vB);
                ptrB = Long.toHexString(pB);
                if (ftB == 1) okB++; else bad++;
            }
            w.println(i + "," + tagA + "," + idA + "," + ptrA
                    + "," + tagB + "," + idB + "," + ptrB);
        }
        w.flush();
        w.close();
        println(String.format("table=%x count=%d colA_ok=%d colB_ok=%d abnormal=%d",
                tableAddr, count, okA, okB, bad));
    }
}
