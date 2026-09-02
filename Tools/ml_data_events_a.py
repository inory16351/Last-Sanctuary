# -*- coding: utf-8 -*-
"""사건 대사 다국어 — event_script 205001~205015 (2026-09-02).

⚠ 이 파일은 <b>자료</b>다. 표에 넣는 것은 `table_update_20260902_multilang_scripts.py` 다.
⚠ 줄바꿈은 <b>실제 개행</b>으로 쓴다 — `gen_string_table.py` 의 `write_tsv` 가
   내보낼 때 `\\n` 리터럴로 접고 런타임의 `StringTable.Unfold` 가 되돌린다.
   한국어·영어 칸은 `\\n` 리터럴로 적혀 있지만 결과는 같다(둘 다 같은 곳으로 접힌다).
★ 낱말은 표의 기존 번역을 따른다 — 성역 Santuario/Sanctuaire/Refugium/聖域/Святилище/
  Santuário/Sanktuarium · 침식 Corrupción/Corruption/Verderbnis/浸食/Порча/Corrupção/Skażenie.
"""

DATA = {

'event_script_205001': dict(
es="En el campo de batalla solo quedan los restos del enemigo.\nTragándose la repulsión que les sube por dentro, los ángeles reúnen los cadáveres.\nQué se hará con esos restos es cosa que decide el Santuario.",
fr="Sur le champ de bataille, il ne reste que les dépouilles de l'ennemi.\nRavalant le dégoût qui monte en eux, les anges rassemblent les cadavres.\nCe qu'il adviendra de ces dépouilles, c'est au Sanctuaire d'en décider.",
de="Auf dem Schlachtfeld bleiben nur die Überreste des Feindes.\nDie Engel schlucken den aufsteigenden Widerwillen hinunter und tragen die Leichen zusammen.\nWas mit diesen Überresten geschieht, entscheidet das Refugium.",
ja="戦場に残されたのは敵の亡骸だけです。\n込み上げる嫌悪を飲み込み、天使たちは骸を一箇所に集めます。\nこの亡骸をどうするかは、聖域が決めることです。",
ru="На поле боя остались только останки врага.\nПроглотив подступающее отвращение, ангелы стаскивают тела в одно место.\nЧто станет с этими останками — решать Святилищу.",
pt="No campo de batalha restam apenas os despojos do inimigo.\nEngolindo a repulsa que lhes sobe por dentro, os anjos juntam os cadáveres.\nO que será feito desses despojos cabe ao Santuário decidir.",
pl="Na polu bitwy zostały tylko szczątki wroga.\nPrzełykając wzbierającą odrazę, anioły znoszą ciała w jedno miejsce.\nCo się stanie z tymi szczątkami, rozstrzygnie Sanktuarium."),

'event_script_205002': dict(
es="Aun después de retirarse la oleada, el calor del Santuario no quiere ceder.\nEl suelo abrasado les quema las plantas a los ángeles y, a la vez, les acelera el pulso.\nHay que decidir cuánto tiempo conservar este calor.",
fr="Même après le reflux de la vague, la chaleur du Sanctuaire refuse de retomber.\nLe sol brûlé consume la plante des pieds des anges tout en accélérant leur pouls.\nIl faut décider combien de temps garder cette chaleur.",
de="Auch nachdem die Welle zurückgewichen ist, will die Hitze des Refugiums nicht abkühlen.\nDer verbrannte Boden versengt den Engeln die Fußsohlen und treibt ihnen zugleich den Puls hoch.\nEs muss entschieden werden, wie lange diese Hitze bewahrt wird.",
ja="ウェーブが引いたあとも、聖域の熱は冷めようとしません。\n焼けた地面は天使たちの足の裏を焦がしながら、同時に脈を速めます。\nこの熱をいつまで抱えておくかを決めなければなりません。",
ru="Даже когда волна отступила, жар Святилища не желает спадать.\nОбожжённая земля жжёт ангелам ступни и в то же время разгоняет им пульс.\nНужно решить, как долго удерживать этот жар.",
pt="Mesmo depois de a onda recuar, o calor do Santuário não quer arrefecer.\nO chão calcinado queima as solas dos anjos e ao mesmo tempo lhes acelera o pulso.\nÉ preciso decidir por quanto tempo guardar este calor.",
pl="Nawet gdy fala opadła, żar Sanktuarium nie chce ostygnąć.\nSpalona ziemia parzy anioły w stopy, a zarazem przyspiesza im tętno.\nTrzeba zdecydować, jak długo zachować ten żar."),

'event_script_205003': dict(
es="Las despensas del Santuario se han quedado secas.\nEs el precio de una lucha larga. A este paso, los ángeles tendrán que recibir la próxima oleada con hambre.",
fr="Les réserves du Sanctuaire se sont taries.\nC'est le prix d'un long combat. À ce rythme, les anges devront affronter la prochaine vague le ventre vide.",
de="Die Vorräte des Refugiums sind versiegt.\nDas ist der Preis eines langen Kampfes. So wird die nächste Welle die Engel hungernd antreffen.",
ja="聖域の蔵が底をつきました。\n長い戦いの代償です。このままでは天使たちは飢えたまま次のウェーブを迎えることになります。",
ru="Кладовые Святилища опустели.\nЭто цена долгого боя. Так ангелы встретят следующую волну голодными.",
pt="Os celeiros do Santuário secaram.\nÉ o preço de uma luta longa. Assim, os anjos terão de enfrentar a próxima onda com fome.",
pl="Spichlerze Sanktuarium wyschły.\nTo cena długiej walki. W tym tempie anioły przyjmą następną falę o głodzie."),

'event_script_205004': dict(
es="En el cuerpo de los ángeles que manejan magia, los gránulos se han hinchado hasta llenarse.\nEs la clase de abundancia que daña a quien la lleva si no se libera.",
fr="Dans le corps des anges qui manient la magie, les granules ont enflé jusqu'à saturation.\nC'est le genre d'abondance qui blesse celui qui la porte si elle n'est pas libérée.",
de="In den Leibern der Engel, die Magie führen, sind die Granula bis zum Rand angeschwollen.\nEs ist jene Art von Fülle, die ihren Träger verletzt, wenn sie nicht entladen wird.",
ja="魔法を扱う天使たちの体の中で、顆粒が満杯まで膨れ上がっています。\n放たなければ持ち主自身を傷つける類の豊かさです。",
ru="В телах ангелов, владеющих магией, гранулы набухли до отказа.\nЭто такое изобилие, которое ранит своего носителя, если его не выпустить.",
pt="No corpo dos anjos que manejam magia, os grânulos incharam até encher.\nÉ o tipo de abundância que fere quem a carrega se não for liberada.",
pl="W ciałach aniołów władających magią ziarnistości nabrzmiały do pełna.\nTo taki nadmiar, który rani nosiciela, jeśli się go nie uwolni."),

'event_script_205005': dict(
es="Las armas que partieron incontables huesos enemigos han perdido el filo.\nNo hay tiempo suficiente ni manos suficientes para repasarlas.",
fr="Les armes qui ont fendu d'innombrables os ennemis ont perdu leur tranchant.\nIl n'y a ni assez de temps ni assez de mains pour les remettre en état.",
de="Die Waffen, die unzählige Feindesknochen gespalten haben, sind stumpf geworden.\nEs fehlt an Zeit wie an Händen, sie zu richten.",
ja="数えきれぬ敵の骨を割ってきた武器が、刃を失いました。\n手入れをする時間も、手入れをする手も足りません。",
ru="Оружие, расколовшее бессчётные вражьи кости, затупилось.\nНи времени, ни рук, чтобы привести его в порядок, не хватает.",
pt="As armas que partiram incontáveis ossos inimigos perderam o fio.\nNão há tempo nem mãos que cheguem para cuidar delas.",
pl="Broń, która rozłupała niezliczone wrogie kości, straciła ostrze.\nNie starcza ani czasu, ani rąk, by ją naprawić."),

'event_script_205006': dict(
es="En el lugar del que se retiró la oleada ha quedado una marca extraña.\nAlguien —o algo— ha dejado un rastro que apunta hacia el corazón del Santuario.",
fr="À l'endroit d'où la vague s'est retirée, une marque étrange est restée.\nQuelqu'un — ou quelque chose — a laissé une piste qui mène vers le cœur du Sanctuaire.",
de="Dort, wo die Welle zurückwich, ist ein fremdes Zeichen zurückgeblieben.\nJemand — oder etwas — hat eine Spur gelegt, die auf das Herz des Refugiums weist.",
ja="ウェーブが退いた場所に、見慣れない印が残されていました。\n誰かが——あるいは何かが——聖域の奥へ向かう道しるべを置いていったのです。",
ru="Там, где отступила волна, осталась странная метка.\nКто-то — или что-то — оставил след, ведущий к сердцу Святилища.",
pt="No lugar de onde a onda recuou ficou uma marca estranha.\nAlguém — ou alguma coisa — deixou um rastro apontando para o coração do Santuário.",
pl="W miejscu, z którego cofnęła się fala, został dziwny znak.\nKtoś — albo coś — zostawił trop wiodący ku sercu Sanktuarium."),

'event_script_205007': dict(
es="Los ángeles que rechazaron la oleada se dan palmadas en el hombro.\nEl sabor de la victoria es dulce, y lo dulce rara vez dura.",
fr="Les anges qui ont repoussé la vague se donnent des tapes sur l'épaule.\nLe goût de la victoire est doux, et ce qui est doux dure rarement.",
de="Die Engel, welche die Welle zurückschlugen, klopfen einander auf die Schulter.\nDer Geschmack des Sieges ist süß, und Süßes hält selten lange.",
ja="ウェーブを押し返した天使たちが互いの肩を叩いています。\n勝利の味は甘く、甘いものはたいてい長くもちません。",
ru="Ангелы, отбившие волну, хлопают друг друга по плечу.\nВкус победы сладок, а сладкое редко держится долго.",
pt="Os anjos que repeliram a onda batem no ombro uns dos outros.\nO gosto da vitória é doce, e o que é doce raramente dura.",
pl="Anioły, które odparły falę, klepią się nawzajem po ramieniu.\nSmak zwycięstwa jest słodki, a słodycz rzadko trwa długo."),

'event_script_205008': dict(
es="Donde se retiraron los restos, brilla un fragmento del enemigo.\nEs material que no pertenece al Santuario. Podrá servir, pero nadie sabe si el cuerpo lo aceptará.",
fr="Là où les dépouilles ont été dégagées, un fragment de l'ennemi luit.\nC'est une matière qui n'appartient pas au Sanctuaire. Elle peut servir, mais nul ne sait si le corps l'acceptera.",
de="Wo die Überreste fortgeräumt wurden, schimmert ein Splitter des Feindes.\nEs ist Stoff, der nicht zum Refugium gehört. Brauchbar vielleicht — ob er dem Leib bekommt, weiß niemand.",
ja="亡骸を片づけた跡に、敵の欠片が光っています。\n聖域のものではない材料です。使えはするでしょうが、体に合うかどうかは誰にもわかりません。",
ru="Там, где убрали останки, поблёскивает осколок врага.\nЭто вещество не принадлежит Святилищу. Пригодиться может, но подойдёт ли телу — не знает никто.",
pt="Onde os despojos foram retirados, brilha um fragmento do inimigo.\nÉ material que não pertence ao Santuário. Pode servir, mas ninguém sabe se o corpo o aceitará.",
pl="Tam, gdzie uprzątnięto szczątki, połyskuje odłamek wroga.\nTo materiał nienależący do Sanktuarium. Da się go użyć, ale nikt nie wie, czy ciało go przyjmie."),

'event_script_205009': dict(
es="Los ángeles de la primera línea han quedado demasiado malheridos.\nEs momento de decidir si las manos que esperan detrás deben adelantarse, o si el frente aguantará un poco más.",
fr="Les anges de première ligne sont trop gravement blessés.\nIl est temps de décider si les mains restées en arrière doivent s'avancer, ou si le front tiendra encore un peu.",
de="Die Engel an vorderster Front sind zu schwer verwundet.\nEs gilt zu entscheiden, ob die Hände dahinter vortreten sollen oder ob die vordere Reihe noch ein wenig hält.",
ja="前線の天使たちの傷が深すぎます。\n後ろに控えた手を前に出すか、前列がもう少し耐えるかを決める時です。",
ru="Ангелы на переднем крае изранены слишком тяжело.\nПора решить: выйти ли вперёд тем, кто стоит позади, или передней линии продержаться ещё немного.",
pt="Os anjos da linha da frente estão feridos demais.\nÉ hora de decidir se as mãos que esperam atrás devem avançar, ou se a frente aguenta mais um pouco.",
pl="Anioły z pierwszej linii są zbyt ciężko ranne.\nPora zdecydować, czy ręce czekające z tyłu mają wyjść naprzód, czy przód wytrzyma jeszcze chwilę."),

'event_script_205010': dict(
es="Hay una herida que no cierra por mucho que se lave.\nAlgo del enemigo permanece en el cuerpo de un ángel, creciendo en silencio.",
fr="Il est une plaie qui ne se referme pas, si souvent qu'on la lave.\nQuelque chose de l'ennemi demeure dans le corps d'un ange, et croît en silence.",
de="Es gibt eine Wunde, die nicht heilt, so oft man sie auch auswäscht.\nEtwas vom Feind bleibt im Leib eines Engels und wächst dort still weiter.",
ja="いくら洗っても塞がらない傷があります。\n敵のものが天使の体に残り、静かに育っています。",
ru="Есть рана, что не затягивается, сколько её ни промывай.\nНечто вражье осталось в теле ангела и тихо растёт.",
pt="Há uma ferida que não fecha por mais que se lave.\nAlgo do inimigo permanece no corpo de um anjo, crescendo em silêncio.",
pl="Jest rana, która nie chce się zamknąć, choćby ją nie wiadomo jak przemywać.\nCoś z wroga zostało w ciele anioła i po cichu rośnie."),

'event_script_205011': dict(
es="Aunque la oleada ha terminado, los ángeles no sueltan las armas.\nSe ve a algunos descargar el filo contra el aire vacío, donde no hay enemigo alguno.",
fr="La vague est passée, et pourtant les anges ne déposent pas leurs armes.\nOn en voit frapper l'air vide, là où ne se tient aucun ennemi.",
de="Die Welle ist vorüber, doch die Engel legen die Waffen nicht nieder.\nManche sieht man ins Leere schlagen, wo gar kein Feind steht.",
ja="ウェーブは終わったのに、天使たちは武器を下ろしません。\n敵のいない空を斬りつけている者も見えます。",
ru="Волна прошла, а ангелы всё не опускают оружия.\nНекоторых видно, как они рубят пустой воздух, где нет никакого врага.",
pt="A onda passou, e ainda assim os anjos não baixam as armas.\nVeem-se alguns golpeando o ar vazio, onde não há inimigo nenhum.",
pl="Fala minęła, a anioły wciąż nie opuszczają broni.\nWidać takich, co tną puste powietrze tam, gdzie nie stoi żaden wróg."),

'event_script_205012': dict(
es="Han vuelto los ángeles que salieron más allá de la niebla.\nAlgunos no regresaron, y en los ojos de los que sí quedó algo que no debió verse nunca.",
fr="Les anges partis au-delà du brouillard sont revenus.\nCertains ne sont pas rentrés, et dans les yeux de ceux qui le sont demeure ce qui n'aurait jamais dû être vu.",
de="Die Engel, die jenseits des Nebels ausgezogen waren, sind zurück.\nEinige kamen nicht wieder, und in den Augen der Heimgekehrten blieb etwas, das niemand hätte sehen dürfen.",
ja="霧の向こうへ出ていった天使たちが戻ってきました。\n何人かは戻れず、戻った者の目には見てはならないものが残っています。",
ru="Ангелы, ушедшие за туман, вернулись.\nНекоторые не дошли обратно, а в глазах вернувшихся осталось то, чего видеть не следовало.",
pt="Voltaram os anjos que saíram para além da névoa.\nAlguns não regressaram, e nos olhos dos que voltaram ficou algo que jamais deveria ter sido visto.",
pl="Wróciły anioły, które wyszły poza mgłę.\nNiektóre nie wróciły, a w oczach tych, co wrócili, zostało coś, czego nie wolno było oglądać."),

'event_script_205013': dict(
es="El suelo del Santuario se ha endurecido y agarra los pies de los ángeles.\nNo es distinto de un tejido enfermo largo tiempo que al fin se vuelve cicatriz.",
fr="Le sol du Sanctuaire s'est durci et retient les pieds des anges.\nCe n'est pas différent d'un tissu longtemps malade qui finit par se changer en cicatrice.",
de="Der Boden des Refugiums ist hart geworden und hält den Engeln die Füße fest.\nEs ist nichts anderes, als wenn lang krankes Gewebe endlich zur Narbe wird.",
ja="聖域の地面が硬く固まり、天使たちの足を掴んでいます。\n長く病んだ組織がついに瘢痕になるのと変わりません。",
ru="Земля Святилища затвердела и держит ангелов за ноги.\nЭто ничем не отличается от того, как долго больная ткань наконец обращается в рубец.",
pt="O chão do Santuário endureceu e prende os pés dos anjos.\nNão é diferente de um tecido há muito doente que enfim vira cicatriz.",
pl="Ziemia Sanktuarium stwardniała i trzyma anioły za stopy.\nNie różni się to od długo chorej tkanki, która w końcu zmienia się w bliznę."),

'event_script_205014': dict(
es="Han brotado nutrientes desde lo hondo del Santuario.\nNo es mucho, pero basta para calmar el hambre.",
fr="Des nutriments sont remontés des profondeurs du Sanctuaire.\nCe n'est pas grand-chose, mais assez pour apaiser la faim.",
de="Aus der Tiefe des Refugiums sind Nährstoffe aufgestiegen.\nViel ist es nicht, doch genug, um den Hunger zu stillen.",
ja="聖域の奥から養分が湧き上がってきました。\n潤沢ではありませんが、飢えをしのぐには足ります。",
ru="Из глубины Святилища поднялись питательные соки.\nНемного, но хватит, чтобы утолить голод.",
pt="Ergueram-se nutrientes do fundo do Santuário.\nNão é muito, mas chega para acalmar a fome.",
pl="Z głębi Sanktuarium podniosły się substancje odżywcze.\nNiewiele tego, ale wystarczy, by uśmierzyć głód."),

'event_script_205015': dict(
es="Entre los restos, un ángel se pone en pie.\nLleva el rostro de un aliado. Pero nadie recuerda desde cuándo estaba ahí tendido.",
fr="Parmi les dépouilles, un ange se relève.\nIl porte le visage d'un allié. Mais nul ne se rappelle depuis quand il gisait là.",
de="Zwischen den Überresten richtet sich ein Engel auf.\nEr trägt das Gesicht eines Verbündeten. Doch niemand erinnert sich, seit wann er dort lag.",
ja="亡骸の中から、天使がひとり立ち上がります。\n味方の顔をしています。ただ、いつからそこに倒れていたのかを誰も覚えていません。",
ru="Среди останков поднимается один ангел.\nУ него лицо своего. Только никто не помнит, с каких пор он там лежал.",
pt="Entre os despojos, um anjo se põe de pé.\nTem o rosto de um aliado. Mas ninguém se lembra desde quando estava ali caído.",
pl="Spośród szczątków podnosi się jeden anioł.\nMa twarz sojusznika. Tyle że nikt nie pamięta, od kiedy tam leżał."),

}
