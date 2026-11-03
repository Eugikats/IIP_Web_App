# IIP claim process image fixtures

Every image in this folder is synthetic test material. These files are not real claimant evidence, insurer forms, or coverage guidance. Use them only in a test workspace and identify the linked claim as synthetic.

The sample images exercise attachment, claim checklist matching, authorized image preview, and human review. They are photographs rather than structured forms, so automated text extraction may correctly return no fields. Reviewers should not treat image labels, damage appearance, or model output as a coverage, liability, fraud, repair-cost, or payment decision.

| File | Suggested product line | Document type | Scenario |
| --- | --- | --- | --- |
| `exec-e7ef8d1b-8164-4cb5-8272-e2da6aad42f6.png` | Motor | PhotoEvidence | Vehicle rear and registration context |
| `exec-ce85a721-4a26-4c85-9459-aa44a5fd9462.png` | Motor | PhotoEvidence | Front impact damage |
| `exec-c47912a3-a9b7-481c-8f56-03a260651207.png` | Motor | PhotoEvidence | Close view of front lamp and damage |
| `exec-519b4240-1148-478d-afa5-6dd82873e25c.png` | Motor | PhotoEvidence | Additional front damage view |
| `exec-bc02334c-c13f-42e5-ac56-71ec24cda9a1.png` | Goods in transit | PhotoEvidence | Loaded freight vehicle |
| `exec-2ea85599-a5c7-4ff3-98f1-36610fb2b402.png` | Goods in transit | PhotoEvidence | Damaged tile cargo |
| `exec-fa0adcaf-9ab2-41e3-acda-1a75e88e15ff.png` | Fire | PhotoEvidence | Fire-damaged shop interior |
| `exec-b4a2915c-f4ea-4067-9a8c-69ae4a99119e.png` | Fire | PhotoEvidence | Close view of damaged stock |
| `exec-8aa5d10e-435f-4f74-a577-ec7571c73f08.png` | Fire | PhotoEvidence | Fire-damaged storefront |

`manifest.json` records these suggested labels in machine-readable form. The uploader asks the operator to confirm a document type; the manifest does not create claims or alter production data.

For structured motor-claim extraction examples, use the separately marked synthetic files in `../synthetic_packs_pdf/pack01_motor_own_damage/`. Its `clean` and `challenge` variants have their own README and ground-truth files. Keep the challenge discrepancies visible for human review.
