const fs=require('fs');const base='D:/xampp/htdocs/ActEditor/tmp/pokemon-batch';const out=base+'/eyes-c';
const specs={
nidoqueen:{color:[49,74,74],front:[{r:[33,11,3,2],p:[[33,11],[34,11],[34,12],[35,12]]}],back:[{r:[63,15,4,4],p:[[64,15],[65,16],[64,17],[65,18]]}]},
onix:{color:[41,33,33],front:[{r:[5,31,2,2],p:[[6,32]]},{r:[15,31,3,2],p:[[16,32]]}],back:[{r:[91,34,2,2],p:[[92,35]]}]},
politoed:{color:[49,115,0],front:[{r:[13,35,2,3],p:[[14,35],[14,36],[13,37]]},{r:[27,36,5,5],p:[[28,36],[29,36],[27,37],[29,37],[30,37],[27,38],[28,38],[29,38],[31,38],[28,39],[31,39],[29,40],[30,40]]}],back:[{r:[24,38,4,4],p:[[25,38],[26,38],[27,39],[25,40],[26,40],[26,41],[27,41]]}]},
psyduck:{color:[148,90,16],front:[{r:[10,12,3,3],p:[[11,13]]},{r:[21,13,3,3],p:[[22,14]]}],back:[]},
roserade:{color:[41,82,82],front:[],back:[]},
scizor:{color:[99,41,41],front:[{r:[36,19,2,2],p:[[36,20]]}],back:[]},
skarmory:{color:[49,82,123],front:[{r:[9,29,5,4],p:[[11,30],[11,31]]}],back:[]},
spiritomb:{color:[82,66,82],front:[{r:[33,32,3,3],p:[[34,33]]}],back:[]},
starmie:{color:[49,58,115],front:[],back:[]},staryu:{color:[90,66,49],front:[],back:[]},
sudowoodo:{color:[99,90,74],front:[{r:[40,17,1,3],p:[[40,18]]},{r:[44,17,1,3],p:[[44,18]]}],back:[]},
togekiss:{color:[74,90,115],front:[{r:[25,20,4,6],p:[[27,21],[27,22],[26,23],[27,23],[26,24]]}],back:[]},
umbreon:{color:[41,49,74],front:[{r:[7,20,2,3],p:[[7,21],[7,22]]}],back:[]},
vaporeon:{color:[16,74,99],front:[{r:[19,20,2,3],p:[[20,21],[20,22]]},{r:[27,20,3,3],p:[[28,20],[29,20],[29,21]]}],back:[]}
};
function add(n,s,e,f,r,p){let eye=specs[n][s][e];if(!eye.refs)eye.refs=[{f:0,r:eye.r,p:eye.p}];eye.refs.push({f,r,p})}
add('nidoqueen','front',0,12,[32,15,4,2],[[33,15],[32,16],[33,16],[34,16]]);
add('nidoqueen','front',0,14,[31,16,5,3],[[31,17],[32,17],[33,17]]);
specs.psyduck.front[0].r=[10,13,3,1];specs.psyduck.front[1].r=[21,14,3,1];
add('psyduck','front',0,4,[8,15,4,1],[[9,15],[10,15]]);
add('scizor','front',0,5,[38,21,2,2],[[38,21]]);
add('spiritomb','front',0,8,[34,30,3,3],[[35,31]]);
add('spiritomb','front',0,22,[33,29,3,3],[[34,30]]);
add('spiritomb','front',0,24,[32,28,3,3],[[33,29]]);
add('togekiss','front',0,6,[28,18,2,3],[[29,18],[29,19],[28,20],[29,20]]);
add('togekiss','front',0,8,[28,17,3,3],[[30,17],[28,18],[30,18],[28,19],[29,19]]);
add('vaporeon','front',0,24,[19,19,2,3],[[20,19],[20,20],[20,21]]);
function load(n,s){let m=JSON.parse(fs.readFileSync(`${base}/probe/${n}-${s}.json`,'utf8').replace(/^\uFEFF/,''));let b=fs.readFileSync(`${base}/probe/${n}-${s}.bgra`);return{m,b}}
function ink(b,i){return b[i+3]>0&&b[i]===b[i+1]&&b[i+1]===b[i+2]&&b[i]<=16}
function patch(b,m,f,r){let a=[];for(let y=r[1];y<r[1]+r[3];y++)for(let x=r[0];x<r[0]+r[2];x++){let i=(f*m.width*m.height+y*m.width+x)*4;a.push(b.readUInt32LE(i))}return a}
let result={},failures=[];
for(const[n,spec]of Object.entries(specs)){let config={name:n,color:spec.color};for(const side of['front','back']){const{m,b}=load(n,side);const eyes=spec[side].map(t=>({prev:[t.r[0],t.r[1]],refs:(t.refs||[{f:0,r:t.r,p:t.p}]).map(ref=>({...ref,values:patch(b,m,ref.f,ref.r)}))}));let frames=[];let miss=0;
for(let f=0;f<m.count;f++){let protectedPixels=[];for(let t=0;t<eyes.length;t++){const eye=eyes[t];let candidates=[];for(const ref of eye.refs){for(let y=Math.max(0,eye.prev[1]-8);y<=Math.min(m.height-ref.r[3],eye.prev[1]+8);y++)for(let x=Math.max(0,eye.prev[0]-8);x<=Math.min(m.width-ref.r[2],eye.prev[0]+8);x++){let equal=true,k=0;for(let yy=0;yy<ref.r[3]&&equal;yy++)for(let xx=0;xx<ref.r[2];xx++,k++){let i=(f*m.width*m.height+(y+yy)*m.width+x+xx)*4;if(b.readUInt32LE(i)!==ref.values[k]){equal=false;break}}if(equal){let d=(x-eye.prev[0])**2+(y-eye.prev[1])**2;candidates.push({x,y,d,ref})}}}candidates.sort((a,b)=>a.d-b.d);if(candidates.length){let c=candidates[0];eye.prev=[c.x,c.y];for(const [px,py]of c.ref.p){let x=px+c.x-c.ref.r[0],y=py+c.y-c.ref.r[1],pi=y*m.width+x;if(!ink(b,(f*m.width*m.height+pi)*4))throw Error(`${n} ${side} ${f}: bad mask ${x},${y}`);protectedPixels.push(pi)}}else{miss++;failures.push({name:n,side,f,eye:t,prev:eye.prev});}}
frames.push([...new Set(protectedPixels)].sort((a,b)=>a-b));}config[side]={width:m.width,height:m.height,frames};result[n+'-'+side]={frames:m.count,eyes:eyes.length,miss};}config.notes='Manual pupil reference patches and pose variants; exact full-color local matching. Unmatched frames require review.';config.reviewed=false;if(!['nidoqueen','psyduck','roserade','scizor','skarmory','starmie','staryu','sudowoodo','togekiss','umbreon','vaporeon','politoed'].includes(n))fs.writeFileSync(`${out}/${n}.json`,JSON.stringify(config));}
fs.writeFileSync(`${out}/failures.json`,JSON.stringify(failures));fs.writeFileSync(`${out}/specs.json`,JSON.stringify(specs));console.log(JSON.stringify(result,null,2));console.log('failures',failures.length);

