const a=require('./analyze'),specs=require('./specs');
for(let [n,sp] of Object.entries(specs))for(let s of ['front','back'])if(sp[s])a.trackedSheet(n,s,sp[s].box,sp[s].mask,n+'-'+s+'-tracked',14);
