using System;
using System.Collections.Generic;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Environment
{
    /// <summary>V4: bounded bodies with hydrostatic cross sections and a globally reproducible spill profile.</summary>
    internal sealed class ContainedHydrology
    {
        internal const double Depth = 2.5, BankGrade = 1.25, RiverCore = 2;
        // One logical cell is one sample interval. Four intervals exceed two cell diagonals,
        // separating wet clipping triangles from the water-field ownership boundary.
        internal const double DryBuffer = 4;
        // Design limit for a static river without waterfalls: elevation units/logical interval.
        // At H2/V1.2 this is atan(.25*1.2/2)=8.53 degrees, independent of chunk size.
        internal const double MaximumRiverGrade = .25;
        readonly WorldDefinition world;
        readonly float sea;
        readonly TerrainGenerator terrain = new TerrainGenerator();
        readonly Dictionary<(long,long), Region> cache = new Dictionary<(long,long), Region>();

        sealed class Region
        {
            public double AX, AZ, BX, BZ, ARadius, BRadius, ALevel, BLevel;
            public bool River;
            public RiverReach Reach;
        }
        public ContainedHydrology(WorldDefinition world, float sea) { this.world=world;this.sea=sea; }
        static long FloorRegion(long p) => p/256-(p%256<0?1:0);
        static double WetRadius(double core) => core+Depth/BankGrade;
        static double Domain(double core) => WetRadius(core)+DryBuffer;

        public bool TryGetRiver(long x,long z,out RiverReach reach)
        { var r=Get(FloorRegion(x),FloorRegion(z));reach=r.Reach;return r.River; }

        double LakeCapacity(double x,double z,double core)
        {
            double wet=WetRadius(core),outer=Domain(core)+2,level=100;
            for(long gz=(long)Math.Floor(z-outer);gz<=(long)Math.Ceiling(z+outer);gz++)
            for(long gx=(long)Math.Floor(x-outer);gx<=(long)Math.Ceiling(x+outer);gx++)
            {
                double dx=gx-x,dz=gz-z,d=Math.Sqrt(dx*dx+dz*dz);
                if(d>=wet && d<=outer)level=Math.Min(level,terrain.SampleElevation(world,gx,gz));
            }
            return Math.Max(sea,level);
        }

        Region Get(long rx,long rz)
        {
            if(cache.TryGetValue((rx,rz),out var region))return region;
            double ax=rx*256.0+60+EnvironmentNoise.Hash(world.Seed,rx,rz,8)*12,az=rz*256.0+60;
            double bx=rx*256.0+180,bz=rz*256.0+184;
            double a=LakeCapacity(ax,az,12),b=LakeCapacity(bx,bz,18);
            region=a>=b ? new Region{AX=ax,AZ=az,BX=bx,BZ=bz,ARadius=16,BRadius=24,ALevel=a,BLevel=b}
                :new Region{AX=bx,AZ=bz,BX=ax,BZ=az,ARadius=24,BRadius=16,ALevel=b,BLevel=a};
            region.River=region.ALevel>sea+3 && region.ALevel-region.BLevel>1;
            var reach=new RiverReach(region.AX,region.AZ,region.BX,region.BZ,region.ALevel,region.BLevel,region.ARadius,region.BRadius);
            if(region.River)
            {
                double length=reach.Length;
                int count=(int)Math.Ceiling(length);
                double step=length/count;
                var caps=new double[count+1];
                for(int i=0;i<=count;i++)caps[i]=region.ALevel;
                // Every dry-bank grid vertex constrains both endpoints of its interpolation
                // segment. Therefore its interpolated water level cannot exceed base ground.
                double padding=Domain(RiverCore)+2;
                for(long z=(long)Math.Floor(Math.Min(region.AZ,region.BZ)-padding-8);z<=(long)Math.Ceiling(Math.Max(region.AZ,region.BZ)+padding+8);z++)
                for(long x=(long)Math.Floor(Math.Min(region.AX,region.BX)-padding-8);x<=(long)Math.Ceiling(Math.Max(region.AX,region.BX)+padding+8);x++)
                {
                    Coordinates(reach,x,z,out double t,out double distance);
                    if(distance<WetRadius(RiverCore) || distance>padding)continue;
                    double position=Math.Max(0,Math.Min(count,t*count));
                    int i=Math.Min(count-1,(int)position);
                    double cap=Math.Max(sea,terrain.SampleElevation(world,x,z));
                    caps[i]=Math.Min(caps[i],cap);caps[i+1]=Math.Min(caps[i+1],cap);
                }
                int first=Math.Min(count,(int)Math.Ceiling((Domain(region.ARadius*.75)+2)/step));
                int last=Math.Max(first,(int)Math.Floor((length-Domain(region.BRadius*.75)-2)/step));
                double source=region.ALevel,receiver=region.BLevel;
                for(int i=0;i<=first;i++)source=Math.Min(source,caps[i]);
                for(int i=last;i<=count;i++)receiver=Math.Min(receiver,caps[i]);
                for(int i=0;i<=first;i++)caps[i]=source;
                for(int i=last;i<=count;i++)caps[i]=receiver;
                // Prefix minimum enforces downhill flow; backward Lipschitz envelope limits
                // longitudinal grade. Flat endpoint windows make lake joins level, not ramps.
                for(int i=1;i<=count;i++)caps[i]=Math.Min(caps[i],caps[i-1]);
                for(int i=count-1;i>=0;i--)
                    caps[i]=Math.Min(caps[i],caps[i+1]+(i<first || i>=last?0:MaximumRiverGrade*step));
                region.ALevel=caps[0];region.BLevel=caps[count];
                reach=new RiverReach(region.AX,region.AZ,region.BX,region.BZ,region.ALevel,region.BLevel,
                    region.ARadius,region.BRadius,caps);
            }
            region.Reach=reach;
            if(cache.Count>=9)cache.Clear();
            cache.Add((rx,rz),region);return region;
        }

        internal static void Coordinates(RiverReach reach,double x,double z,out double t,out double distance)
        {
            double dx=reach.BX-reach.AX,dz=reach.BZ-reach.AZ,length=reach.Length;
            double ax=x-reach.AX,az=z-reach.AZ;
            t=(ax*dx+az*dz)/(length*length);
            double clamped=Math.Max(0,Math.Min(1,t));
            double across=(ax*-dz+az*dx)/length-Math.Sin(clamped*Math.PI)*8;
            double beyond=(t-clamped)*length;
            distance=Math.Sqrt(across*across+beyond*beyond);
        }

        public void Apply(long x,long z,ref double elevation,ref double water,ref WaterKind kind)
        {
            long rx=FloorRegion(x),rz=FloorRegion(z);
            // Max bed influence: core18 + (100+Depth)/BankGrade=100 logical units.
            const double support=100;
            for(int iz=-1;iz<=1;iz++)for(int ix=-1;ix<=1;ix++)
            {
                long tx=rx+ix,tz=rz+iz;double ox=tx*256.0,oz=tz*256.0;
                if(x<ox+60-support || x>ox+184+support || z<oz+60-support || z>oz+184+support)continue;
                var r=Get(tx,tz);
                double ax=x-r.AX,az=z-r.AZ,bx=x-r.BX,bz=z-r.BZ;
                Field(Math.Sqrt(ax*ax+az*az),r.ARadius*.75,r.ALevel,WaterKind.Lake,ref elevation,ref water,ref kind);
                Field(Math.Sqrt(bx*bx+bz*bz),r.BRadius*.75,r.BLevel,WaterKind.Lake,ref elevation,ref water,ref kind);
                if(!r.River)continue;
                Coordinates(r.Reach,x,z,out double t,out double distance);
                Field(distance,RiverCore,r.Reach.LevelAt(Math.Max(0,Math.Min(1,t))),WaterKind.River,ref elevation,ref water,ref kind);
            }
        }
        void Field(double distance,double core,double level,WaterKind type,ref double elevation,ref double water,ref WaterKind kind)
        {
            elevation=Math.Min(elevation,level-Depth+BankGrade*Math.Max(0,distance-core));
            // Ownership changes only in the certified dry buffer, never by tilting water.
            if(distance<=Domain(core) && level>water){water=level;kind=type;}
        }
    }
}
