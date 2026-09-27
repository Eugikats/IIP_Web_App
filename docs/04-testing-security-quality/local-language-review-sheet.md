# Local-language review sheet

For a native speaker of Luganda and of Kiswahili. Real output from the live translation engine on the dev environment,
produced on 2026-09-27 through the same service the WhatsApp assistant uses. Nothing here has been reviewed yet.

## How to score

Write one word in the **Score** column of every row:

- **correct**: a native speaker would say it the same way, and it means what the English means.
- **understandable**: awkward, or not how you would say it, but a customer would not be misled.
- **wrong**: it changes the meaning, invents something, or a customer could be misled or offended.

Where you can, write the sentence you would use in the **Better version** column. Those become the reviewed
replacements that customers receive instead of the machine translation.

## What to know first

- Part A is what customers **receive**: the assistant's real replies, translated from English. This is the part that
  can mislead someone, so it matters most. Luganda keeps the words *claim*, *policy* and *claims officer* in English on
  purpose: earlier tests turned them into unrelated words.
- Part B is what customers **write**: the engine's English reading of each message. The Swahili and Luganda messages were
  written by the assistant, not by a native speaker, so replace any that no customer would really write.
- Every reply a customer receives says a computer translated it, and no reply is treated as approved until you have scored it.

## Kiswahili

### Part A: replies the assistant sends in Kiswahili

| # | English (what the assistant means) | Kiswahili (engine output) | Score | Better version |
|---|---|---|---|---|
| 1 | Hello, thanks for getting in touch. I can help you report a claim, check on a claim you've already made, or answer questions about your cover. What can I do for you today? | Halo, asante kwa kuwasiliana nami. Ninaweza kukusaidia kuripoti madai, kuchunguza madai uliyowahi kutoa, au kujibu maswali kuhusu bima yako. Ninaweza kukusaidiaje leo? | | |
| 2 | Thank you for your question. I've passed it to our team to confirm the answer, and someone will reply to you here shortly. | Asante kwa swali lako. Nimeipeleka kwa timu yetu ili kuthibitisha jibu, na mtu atakujibu hapa hivi karibuni. | | |
| 3 | I'm not able to help with that directly, I've passed this to our team and someone will follow up with you. | Siwezi kukusaidia moja kwa moja kwa hilo, nimepeleka hili kwa timu yetu na mtu fulani atafuatilia na wewe. | | |
| 4 | I'm the automated assistant for our claims team, so I can help any time of day. If you'd rather talk to a person, just say so and I'll bring someone in. | Mimi ni msaidizi wa kiotomatiki wa timu yetu ya madai, kwa hiyo naweza kusaidia wakati wowote wa siku. Kama unapenda kuzungumza na mtu, sema tu na nitaleta mtu. | | |
| 5 | You're welcome. I'm here whenever you need anything. | Karibu sana. Niko hapa wakati wowote unapohitaji kitu chochote. | | |
| 6 | Sorry, I didn't quite catch that. Are you reporting something that has happened, checking on a claim, or asking about your cover? You can also say "agent" to talk to someone from our team. | Samahani, sikuelewa vizuri. Je, unaripoti jambo fulani lililotukia, kuchunguza madai fulani, au kuuliza kuhusu bima yako? Unaweza pia kusema "agent" kuzungumza na mtu kutoka timu yetu. | | |
| 7 | Let me bring in someone from our team so you get the right help. They'll reply to you here shortly. | Hebu nilete mtu kutoka timu yetu ili upate msaada sahihi. Watakujibu hapa hivi karibuni. | | |
| 8 | I couldn't find a policy matching NSA/MO/2026/00013. Please double-check the number, or say "agent" and someone from our team will help. | Sikuweza kupata sera inayolingana na NSA/MO/2026/00013. Tafadhali rejea namba mara mbili, au sema "agent" na mtu kutoka timu yetu atakusaidia. | | |
| 9 | I'm sorry to hear that. Which cover is this about? Reply with the number or the name. | Nasikitika kusikia hivyo. Hii ni kuhusu kifuniko gani? Jibu na namba au jina. | | |
| 10 | I'm sorry to hear that. To start your claim, please reply with your policy number. It's on your policy schedule or certificate. | Nasikitika kusikia hivyo. Kuanza madai yako, tafadhali jibu na nambari yako ya sera. Ni kwenye ratiba yako ya sera au cheti. | | |
| 11 | I'm sorry to hear that, I hope everyone is safe. Tell me in your own words what happened, when it happened and where, and I'll open the claim on your Motor policy (NSA/MO/2026/00013). | Nasikitika kusikia hivyo, natumaini kila mtu yuko salama. Niambie kwa maneno yako mwenyewe ni nini kilichotokea, ni lini na wapi, nami nitafungua madai kwenye bima yako ya Motori (NSA/MO/2026/00013). | | |
| 12 | Thank you. I've opened your claim on your Motor policy. Your reference is CLM-4E1A92C7. A claims officer will review it and keep you updated here. | Asante. Nimefungua madai yako kwenye bima yako ya Motor. Maelezo yako ni CLM-4E1A92C7. Afisa wa madai atakagua na kukujulisha hapa. | | |
| 13 | To help us move quickly, please send photos of the damage, the police abstract and your driving permit. You can send photos right here in this chat. | Ili kutusaidia kusonga haraka, tafadhali tuma picha za uharibifu, muhtasari wa polisi na kibali chako cha kuendesha gari. Unaweza kutuma picha hapa kwenye mazungumzo haya. | | |
| 14 | You can send photos right here in this chat, or upload them securely at this link, which works for 24 hours: https://portal.example.com/access/verify?token=abc123 | Unaweza kutuma picha hapa kwenye mazungumzo haya, au kuzipakia kwa usalama kwenye kiungo hiki, ambacho kinafanya kazi kwa masaa 24: https://portal.example.com/access/verify?token=abc123 | | |
| 15 | Hi, here's where things stand: your claim CLM-4E1A92C7 is under review. A claims officer will contact you here if they need anything. Reply "link" if you'd like to view your claim or add documents. | Hapa ndipo mambo yalivyo: madai yako CLM-4E1A92C7 yako kwenye ukaguzi. Afisa wa madai atawasiliana nawe hapa ikiwa anahitaji chochote. Jibu "link" kama unataka kuangalia madai yako au kuongeza nyaraka. | | |
| 16 | You don't have any open claims right now. Your most recent claim, CLM-4E1A92C7 (Motor), is settled. If something new has happened, tell me and I'll start a claim. | Huna madai yoyote ya wazi kwa sasa. Madai yako ya hivi karibuni, CLM-4E1A92C7 (Motor), yametatuliwa. Kama kuna kitu kipya kimetokea, niambie na nitaanza madai. | | |
| 17 | I can't see any claims on your policies yet. If something has happened, tell me what and I'll start one for you. | Sijaona madai yoyote kuhusu sera zako bado. Kama kuna kitu kimetokea, niambie ni nini na nitaanza moja kwa ajili yako. | | |
| 18 | To check your claim status, please reply with your policy number so I can verify it's you. | Ili kuangalia hali ya madai yako, tafadhali jibu na nambari yako ya sera ili niweze kuthibitisha ni wewe. | | |
| 19 | I verified policy NSA/MO/2026/00013, but there's no claim on file for it yet. If something has happened, tell me what and I'll start one. | Nilitambua sera hiyo NSA/MO/2026/00013, lakini bado hakuna madai yoyote kuhusu hilo. Kama kuna kitu kimetokea, niambie ni nini na nitaanza moja. | | |
| 20 | Your claim CLM-4E1A92C7 on policy NSA/MO/2026/00013 is currently: Waiting on documents. A claims officer will contact you here if they need anything. | Madai yako CLM-4E1A92C7 kwenye sera NSA/MO/2026/00013 kwa sasa: Inasubiri nyaraka. Afisa wa madai atawasiliana nawe hapa ikiwa anahitaji chochote. | | |
| 21 | I couldn't find an open claim linked to this number. Please reply with your policy number or claim reference, or tell me what happened and I'll start a claim. | Sikuweza kupata madai ya wazi yanayohusiana na namba hii. Naomba kujibu namba yako ya sera au kumbukumbu ya madai, au niambie kilichotokea na nitaanza madai. | | |
| 22 | Here's your secure link for claim CLM-4E1A92C7. It works for 24 hours: https://portal.example.com/access/verify?token=abc123 You can also send photos right here in this chat. | Hapa ni kiungo chako salama cha madai CLM-4E1A92C7. Inafanya kazi kwa masaa 24: https://portal.example.com/access/verify?token=abc123 Unaweza pia kutuma picha hapa kwenye mazungumzo haya. | | |
| 23 | Thank you. I've received your file. To add it to the right claim, please reply with your policy number or claim reference. | Asante. Nimepokea faili yako. Ili kuongeza kwenye madai ya kulia, tafadhali jibu na nambari yako ya sera au kumbukumbu ya madai. | | |
| 24 | Thank you. I've added that to your claim CLM-4E1A92C7. Send anything else you have whenever you're ready. | Asante. Nimeongeza hilo kwenye madai yako CLM-4E1A92C7. Tuma chochote kingine ulicho nacho wakati wowote ukiwa tayari. | | |
| 25 | I don't have an approved answer for that yet, I've passed this to our team and someone will follow up with you. | Sina jibu lililokubaliwa kwa hilo bado, nimepeleka hili kwa timu yetu na mtu fulani atafuatilia na wewe. | | |
| 26 | Thanks for your interest. I've passed your details to our sales team, and someone will reach out with options that fit what you need. | Asante kwa maslahi yako. Nimewasilisha maelezo yako kwa timu yetu ya mauzo, na mtu fulani atakuja na chaguzi zinazolingana na kile unachohitaji. | | |
| 27 | Of course. I've asked a member of our team to pick this up, and they'll reply to you here shortly. | Bila shaka. Nimemwomba mshiriki wa timu yetu achukue hii, na watakujibu hapa hivi karibuni. | | |
| 28 | I can reply in English, Luganda or Kiswahili. Reply 1 for English, 2 for Luganda, 3 for Kiswahili | Ninaweza kujibu kwa Kiingereza, Luganda au Kiswahili. Jibu 1 kwa Kiingereza, 2 kwa Luganda, 3 kwa Kiswahili | | |
| 29 | Your claim documents have been received and your case is ready for review by our claims team. We will keep you updated as the review progresses. | Nyaraka zako za madai zimepokelewa na kesi yako iko tayari kwa ukaguzi na timu yetu ya madai. Tutakujuza kadri ukaguzi utakavyoendelea. | | |
| 30 | A decision has been reached on your claim for policy NSA/MO/2026/00013. A claims officer will contact you directly to discuss it. | Uamuzi umefikiwa kuhusu madai yako ya bima NSA/MO/2026/00013. Ofisa wa madai atawasiliana nawe moja kwa moja ili kuzungumzia jambo hilo. | | |

### Part B: messages customers write in Kiswahili

| # | Kiswahili message | Engine's English reading | Score | Correct English |
|---|---|---|---|---|
| 1 | Habari, naomba kuripoti ajali | Hello, I would like to report an accident | | |
| 2 | Nimepata ajali na gari langu leo asubuhi kwenye barabara ya Jinja | I got into an accident with my car this morning on Jinja Road | | |
| 3 | Sera yangu ni NSA/MO/2026/00013 | My policy is NSA/MO/2026/00013 | | |
| 4 | Gari langu limegongwa na bodaboda jana usiku | My car was hit by a boda boda last night | | |
| 5 | Naomba kujua hali ya madai yangu ya bima | I would like to know the status of my insurance claim. | | |
| 6 | Je, bima yangu inalipia kioo cha gari kilichovunjika? | Does my insurance cover a broken windshield? | | |
| 7 | Nimetuma picha za uharibifu, mmezipokea? | I sent you pictures of the damage, did you receive them? | | |
| 8 | Ninahitaji nyaraka gani ili kudai fidia? | What documents do I need to claim compensation? | | |
| 9 | Polisi wamenipa ripoti ya ajali, nitaituma wapi? | The police have given me an accident report, where should I send it? | | |
| 10 | Asante sana kwa msaada wenu | Thank you very much for your help | | |
| 11 | Nataka kuongea na mtu, si na kompyuta | I want to talk to a person, not a computer | | |
| 12 | Gari langu liliibiwa jana usiku, nifanye nini? | My car was stolen last night, what should I do? | | |
| 13 | Nimeshatuma kibali changu cha kuendesha gari | I've got my driver's license. | | |
| 14 | Madai yangu yamechukua muda mrefu sana | My claim has dragged on for too long | | |
| 15 | Nataka kujua bima ya afya inalipia nini | I want to know what health insurance costs | | |
| 16 | Mume wangu amelazwa hospitalini, ninawezaje kudai? | My husband has been admitted to the hospital, how can I claim? | | |
| 17 | Nimepoteza hati yangu ya bima, naweza kupata nakala? | I lost my insurance policy, can I get a copy? | | |
| 18 | Naomba mnitumie kiungo cha kupakia nyaraka | Please send me the link to upload the documents | | |
| 19 | Nilifanya ajali lakini hakuna aliyejeruhiwa | I did an accident but no one was hurt | | |
| 20 | Ajali ilitokea Ijumaa saa mbili usiku karibu na daraja | The accident occurred on Friday night at about 2am near the bridge | | |
| 21 | Sijui namba ya sera yangu | I don't know my policy number | | |
| 22 | Mtu mwingine aligonga gari langu na akakimbia | Another man hit my car and fled | | |
| 23 | Mnaweza kunipigia simu kesho asubuhi? | Can you call me tomorrow morning? | | |
| 24 | Nimepokea ujumbe wenu, asante | I have received your message, thank you | | |
| 25 | Nataka kubadilisha namba yangu ya simu | I want to change my phone number | | |
| 26 | Bima yangu inaisha lini? | When does my insurance expire? | | |
| 27 | Gari limeharibika vibaya, siwezi kuliendesha | The car is badly damaged, I can't drive it | | |
| 28 | Nimeshalipa ada ya bima mwezi huu | I paid my insurance premium this month | | |
| 29 | Naomba msaada, mtoto wangu amegongwa na gari | I need help, my child is hit by a car | | |
| 30 | Ripoti ya polisi bado haijakamilika, nikutumie baadaye? | The police report is not yet complete, can I send it to you later? | | |

## Luganda

### Part A: replies the assistant sends in Luganda

| # | English (what the assistant means) | Luganda (engine output) | Score | Better version |
|---|---|---|---|---|
| 1 | Hello, thanks for getting in touch. I can help you report a claim, check on a claim you've already made, or answer questions about your cover. What can I do for you today? | Hello, thanks for getting in touch. Nkwebaza nnyo olw'okutuukirira. Nsobola okukuyamba okuwandiika lipoota ya claim, okukebera claim gy'omaze okukola, oba okuddamu ebibuuzo ebikwata ku kivvulu kyo. Nnyinza kukola ki leero? | | |
| 2 | Thank you for your question. I've passed it to our team to confirm the answer, and someone will reply to you here shortly. | Weebale nnyo olw'ekibuuzo kyo. Nkuleetedde ttiimu yaffe okukakasa eky'okuddamu, era omuntu ajja kukuddamu wano mu kiseera ekitali kya wala. | | |
| 3 | I'm not able to help with that directly, I've passed this to our team and someone will follow up with you. | Sisobola kukuyamba butereevu, kino nkitegeezezza ttiimu yaffe era waliwo omuntu agenda kukulondoola. | | |
| 4 | I'm the automated assistant for our claims team, so I can help any time of day. If you'd rather talk to a person, just say so and I'll bring someone in. | Ndi muyambi wa ttiimu yaffe eya claims, n'olwekyo nsobola okubayamba essaawa yonna. Bw'oba oyagala okwogera n'omuntu, yogera nange era nja kuleeta omuntu omulala. | | |
| 5 | You're welcome. I'm here whenever you need anything. | Oyanirizibwa. Ndi wano buli lw'oba weetaaga ekintu kyonna. | | |
| 6 | Sorry, I didn't quite catch that. Are you reporting something that has happened, checking on a claim, or asking about your cover? You can also say "agent" to talk to someone from our team. | Nsonyiwa, ekyo sikitegedde. Olina ky'owandiika ku kintu ekibaddewo, okukebera claim, oba okubuuza ku kifaananyi kyo? Osobola n'okugamba "agent" okwogera n'omuntu okuva mu ttiimu yaffe. | | |
| 7 | Let me bring in someone from our team so you get the right help. They'll reply to you here shortly. | Ka nkuleetere omuntu okuva mu ttiimu yaffe osobole okufuna obuyambi obutuufu. Bajja kukuddamu wano mu kiseera ekitali kya wala. | | |
| 8 | I couldn't find a policy matching NSA/MO/2026/00013. Please double-check the number, or say "agent" and someone from our team will help. | Sisobodde kufuna policy ekwatagana ne NSA/MO/2026/00013. Nkusaba okukebera ennamba eyo, oba yogera "omubaka" era omu ku ttiimu yaffe ajja kukuyamba. | | |
| 9 | I'm sorry to hear that. Which cover is this about? Reply with the number or the name. | Nsonyiwa okuwulira ekyo. Kiki kino kye kikwata ku? Ddamu n'ennamba oba erinnya. | | |
| 10 | I'm sorry to hear that. To start your claim, please reply with your policy number. It's on your policy schedule or certificate. | Nsonyiwa okuwulira ekyo. Okutandika claim, tukusaba oddemu n'ennamba yo policy. Kiri ku kalenda yo policy oba ku kiwandiiko kyo. | | |
| 11 | I'm sorry to hear that, I hope everyone is safe. Tell me in your own words what happened, when it happened and where, and I'll open the claim on your Motor policy (NSA/MO/2026/00013). | Nsonyiwa okuwulira ekyo, nsuubira buli omu ali bulungi. Mbuulira mu bigambo byo ekyabaddewo, ddi lwe kyabaddewo era wa, era nja kuggulawo claim ku Motor yo policy (NSA/MO/2026/00013). | | |
| 12 | Thank you. I've opened your claim on your Motor policy. Your reference is CLM-4E1A92C7. A claims officer will review it and keep you updated here. | Nkwebaza. Nzibudde claim yo ku Motor yo policy. Ekitundu kyo kiri CLM-4E1A92C7. claims officer ejja kukikebera era ekuyambe okumanya ebipya. | | |
| 13 | To help us move quickly, please send photos of the damage, the police abstract and your driving permit. You can send photos right here in this chat. | Okusobola okutuyamba mu bwangu, tukusaba otuwe ebifaananyi by'ebizibu, ekiwandiiko kya poliisi n'ebitabo by'okuvuga. Osobola okusindika ebifaananyi wano mu mboozi eno. | | |
| 14 | You can send photos right here in this chat, or upload them securely at this link, which works for 24 hours: https://portal.example.com/access/verify?token=abc123 | Osobola okusindika ebifaananyi wano mu mboozi eno, oba okubiteeka mu ngeri ey'obukuumi ku linki eno, ekola okumala essaawa 24: https://portal.example.com/access/verify?token=abc123 | | |
| 15 | Hi, here's where things stand: your claim CLM-4E1A92C7 is under review. A claims officer will contact you here if they need anything. Reply "link" if you'd like to view your claim or add documents. | Wano we biri: claim CLM-4E1A92C7 yo eri mu kukeberebwa. claims officer ajja kukutuukirira wano singa baba balina kye beetaaga. Ddamu "link" bw'oba oyagala okulaba claim yo oba okwongerako ebiwandiiko. | | |
| 16 | You don't have any open claims right now. Your most recent claim, CLM-4E1A92C7 (Motor), is settled. If something new has happened, tell me and I'll start a claim. | Tolina claims ey'obutereevu mu kiseera kino. Ekisumuluzo kyo ekisembayo claim, CLM-4E1A92C7 (Motor), kimalirizibwa. Bwe wabaawo ekintu ekippya ekibaddewo, mbuulira era nja kutandikawo claim. | | |
| 17 | I can't see any claims on your policies yet. If something has happened, tell me what and I'll start one for you. | Sisobola kulaba claims yonna ku policies yo. Bwe wabaawo ekintu ekikoleddwa, mbuulira era nja kukikolera. | | |
| 18 | To check your claim status, please reply with your policy number so I can verify it's you. | Okukebera embeera yo claim, nkusaba oddemu n'ennamba yo policy nsobole okukakasa nti ggwe. | | |
| 19 | I verified policy NSA/MO/2026/00013, but there's no claim on file for it yet. If something has happened, tell me what and I'll start one. | Nnakakasa policy NSA/MO/2026/00013, naye tewali claim ku fayiro yaayo. Bwe wabaawo ekintu ekikoleddwa, mbuulira era nja kutandika. | | |
| 20 | Your claim CLM-4E1A92C7 on policy NSA/MO/2026/00013 is currently: Waiting on documents. A claims officer will contact you here if they need anything. | claim CLM-4E1A92C7 yo ku policy NSA/MO/2026/00013 mu kiseera kino: Olinda ebiwandiiko. claims officer ajja kukutuukirira wano singa baba balina kye beetaaga. | | |
| 21 | I couldn't find an open claim linked to this number. Please reply with your policy number or claim reference, or tell me what happened and I'll start a claim. | Sisobodde kufuna claim ey'obutereevu ekwatagana n'ennamba eno. Nkusaba onzikirize n'ennamba yo eya policy oba claim reference, oba ombuulire ebyabaddewo era nja kutandika claim. | | |
| 22 | Here's your secure link for claim CLM-4E1A92C7. It works for 24 hours: https://portal.example.com/access/verify?token=abc123 You can also send photos right here in this chat. | Wano waliwo linki yo ey'obukuumi eya claim CLM-4E1A92C7. Kikola essaawa 24: https://portal.example.com/access/verify?token=abc123 Osobola n'okutumira ebifaananyi wano mu mboozi eno. | | |
| 23 | Thank you. I've received your file. To add it to the right claim, please reply with your policy number or claim reference. | Nkwebaza. Nfunye fayiro yo. Okugattako ku ddyo claim, tukusaba oddemu n'ennamba yo policy oba claim. | | |
| 24 | Thank you. I've added that to your claim CLM-4E1A92C7. Send anything else you have whenever you're ready. | Nkwebaza. Nnyongedde ekyo ku claim CLM-4E1A92C7 yo. Tuma ekintu ekirala kyonna ky'olina buli lw'obeera omwetegefu. | | |
| 25 | I don't have an approved answer for that yet, I've passed this to our team and someone will follow up with you. | Sirina kya kuddamu ku nsonga eyo, nkuwadde ttiimu yaffe era waliwo omuntu agenda kukutuukirira. | | |
| 26 | Thanks for your interest. I've passed your details to our sales team, and someone will reach out with options that fit what you need. | Weebale nnyo olw'okufaayo. Nnabuulirako ttiimu yaffe ey'okutunda ebikukwatako, era waliwo omuntu ajja kukutuusaako ebintu ebituukana n'ebyo by'oyagala. | | |
| 27 | Of course. I've asked a member of our team to pick this up, and they'll reply to you here shortly. | Kya lwatu. Nasabye omu ku ttiimu yaffe okutwala kino, era bajja kukuddamu wano mu kiseera ekitali kya wala. | | |
| 28 | I can reply in English, Luganda or Kiswahili. Reply 1 for English, 2 for Luganda, 3 for Kiswahili | Nsobola okuddamu mu Lungereza, Luganda oba Kiswahili. Okuddamu 1 mu Lungereza, 2 mu Luganda, 3 mu Kiswahili | | |
| 29 | Your claim documents have been received and your case is ready for review by our claims team. We will keep you updated as the review progresses. | Ebiwandiiko byo claim byatwalibwa era omusango gwo gwetegekeddwa okwekenneenyezebwa ttiimu yaffe claims. Tujja kukutegeeza nga okwekenneenya kugenda mu maaso. | | |
| 30 | A decision has been reached on your claim for policy NSA/MO/2026/00013. A claims officer will contact you directly to discuss it. | Okusalawo ku claim kwo ku policy NSA/MO/2026/00013 kutuuse. claims officer ajja kukutuukirira butereevu okukikubaganyaako ebirowoozo. | | |

### Part B: messages customers write in Luganda

| # | Luganda message | Engine's English reading | Score | Correct English |
|---|---|---|---|---|
| 1 | Nfunye akabenje ku motoka yange leero ku makya | I had an accident on my car this morning | | |
| 2 | Nnina akabenje, nsaba mumbeeko | I have an accident, please help me | | |
| 3 | Webale nnyo | Thank you very much | | |
| 4 | Oli otya? Njagala okuwaayo claim | How are you? I would like to make a claim | | |
| 5 | Motoka yange yakoseddwa nnyo | My motor is severely affected | | |
| 6 | Nsaba okumanya embeera ya claim yange | I request to know the status of my claim | | |
| 7 | Nnina ebiwandiiko bya poliisi | I have police records | | |
| 8 | Nsaba mumbuulire ebiwandiiko bye nnina okuwaayo | Please tell me what documents I need to provide | | |
| 9 | Nsobola okuweereza ebifaananyi wa? | Where can I send the photos? | | |
| 10 | Nsonyiwa, sirina namba ya policy yange | Sorry, I don't have my policy number | | |
| 11 | Motoka yange yabbiddwa ekiro ekyayita | My motorcycle was stolen last night | | |
| 12 | Njagala okwogera n'omuntu | I want to talk to someone | | |
| 13 | Nawadde ebifaananyi, mubifunye? | I provided some pictures, did you find them? | | |
| 14 | Claim yange etwala budde bungi nnyo | My claim takes too long | | |
| 15 | Yinsuwa yange esasula ebintu ki? | What do my insurance policies cover? | | |
| 16 | Omwana wange akubiddwa emotoka | My son is hit by a car | | |
| 17 | Nsaba mumpeereze link okuwaayo ebiwandiiko | Please send me a link to donate the documents | | |
| 18 | Akabenje kaabaddewo ku lwokutaano ekiro | The accident occurred at 5pm | | |
| 19 | Tewali yafuna bisago | No one got a bisago | | |
| 20 | Omuntu omulala yakuba motoka yange n'adduka | Someone else hit my car and fled | | |
| 21 | Muyinza okumpita ku ssimu enkya? | Can you call me in the morning? | | |
| 22 | Nfunye obubaka bwammwe, webale | I got your message, thank you | | |
| 23 | Njagala okukyusa namba yange ey'essimu | I would like to change my phone number | | |
| 24 | Yinsuwa yange ejja kuggwaako ddi? | When will my insurance expire? | | |
| 25 | Motoka yange tesobola kutambula | My motor is not moving | | |
| 26 | Nasasudde yinsuwa omwezi guno | I paid my insurance this month | | |
| 27 | Nsaba obuyambi bwammwe | I ask for your help | | |
| 28 | Ripooti ya poliisi tennaggwa, nkuweereze oluvannyuma? | The police report is not finished yet, can I send it to you later? | | |
| 29 | Ndi mu ddwaliro, nkola ntya okusaba claim? | I am in hospital, how do I make a claim? | | |
| 30 | Nnina okukuwa ssente ezisigaddewo? | Should I give you the remaining balance? | | |

## Scoring status

Pending. No native speaker has scored this sheet. Until then, translated replies are switched on in the dev workspace only,
and only because each one is labelled as machine translated.

