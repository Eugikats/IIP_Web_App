"use client";

// The certificate of completion, drawn as a PDF in the browser. It carries the IIP and Otic marks, the
// person's name, the course and the date. It says what it is: a certificate of completion, not an
// accredited qualification.

async function dataUrl(path: string): Promise<{ url: string; width: number; height: number }> {
  const blob = await (await fetch(path)).blob();
  const url = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
  const size = await new Promise<{ width: number; height: number }>((resolve) => {
    const image = new Image();
    image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight });
    image.src = url;
  });
  return { url, ...size };
}

export async function downloadCertificate(input: { name: string; courseTitle: string; completedAt: string; certificateId: string }) {
  const { jsPDF } = await import("jspdf");
  const doc = new jsPDF({ orientation: "landscape", unit: "pt", format: "a4" });
  const width = doc.internal.pageSize.getWidth();
  const height = doc.internal.pageSize.getHeight();
  const navy: [number, number, number] = [11, 22, 64];
  const blue: [number, number, number] = [23, 105, 255];
  const grey: [number, number, number] = [100, 116, 139];

  doc.setDrawColor(...blue);
  doc.setLineWidth(3);
  doc.rect(28, 28, width - 56, height - 56);
  doc.setDrawColor(203, 213, 225);
  doc.setLineWidth(0.8);
  doc.rect(38, 38, width - 76, height - 76);

  const [iip, otic] = await Promise.all([dataUrl("/iip-mark.png"), dataUrl("/otic-logo.png")]);
  const iipHeight = 56;
  doc.addImage(iip.url, "PNG", 70, 66, (iip.width / iip.height) * iipHeight, iipHeight);
  doc.setFont("helvetica", "bold");
  doc.setFontSize(15);
  doc.setTextColor(...navy);
  doc.text("IIP Academy", 70 + (iip.width / iip.height) * iipHeight + 12, 98);
  doc.setFont("helvetica", "normal");
  doc.setFontSize(9.5);
  doc.setTextColor(...grey);
  doc.text("Insurance Intelligence Platform", 70 + (iip.width / iip.height) * iipHeight + 12, 113);

  const oticHeight = 44;
  const oticWidth = (otic.width / otic.height) * oticHeight;
  doc.addImage(otic.url, "PNG", width - 70 - oticWidth, 72, oticWidth, oticHeight);

  doc.setFont("helvetica", "bold");
  doc.setFontSize(34);
  doc.setTextColor(...navy);
  doc.text("Certificate of completion", width / 2, 218, { align: "center" });

  doc.setFont("helvetica", "normal");
  doc.setFontSize(13);
  doc.setTextColor(...grey);
  doc.text("This certifies that", width / 2, 262, { align: "center" });

  doc.setFont("helvetica", "bold");
  doc.setFontSize(32);
  doc.setTextColor(...blue);
  doc.text(input.name || "Name not given", width / 2, 312, { align: "center", maxWidth: width - 160 });
  const nameWidth = Math.min(width - 160, doc.getTextWidth(input.name || "Name not given"));
  doc.setDrawColor(...blue);
  doc.setLineWidth(1);
  doc.line(width / 2 - nameWidth / 2 - 10, 324, width / 2 + nameWidth / 2 + 10, 324);

  doc.setFont("helvetica", "normal");
  doc.setFontSize(13);
  doc.setTextColor(...grey);
  doc.text("has completed the course", width / 2, 356, { align: "center" });

  doc.setFont("helvetica", "bold");
  doc.setFontSize(20);
  doc.setTextColor(...navy);
  doc.text(input.courseTitle, width / 2, 392, { align: "center", maxWidth: width - 180 });

  const date = new Date(input.completedAt).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" });
  doc.setFont("helvetica", "normal");
  doc.setFontSize(11);
  doc.setTextColor(...grey);
  doc.text(`Completed on ${date}`, 70, height - 82);
  doc.text(`Certificate number ${input.certificateId}`, width - 70, height - 82, { align: "right" });
  doc.setFontSize(8.5);
  doc.text("A certificate of completion for training delivered in IIP Academy. It is not an accredited qualification.", width / 2, height - 58, { align: "center" });

  doc.save(`IIP-Academy-${input.courseTitle.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "")}.pdf`);
}
