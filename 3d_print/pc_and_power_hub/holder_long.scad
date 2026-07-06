$fn = $preview ? 36 : 360;

use <cutouts/screws.scad>

// Distance between PC holes
d_pc_holes = 80;

// Height of PC holes
h_pc_holes = 47;

// Hole diameter (M3)
D_pc_hole = 3.25;

// Washer width
D_pc_washer = 9 + 0.5;

// Width of PC horns
w_pc_horns = D_pc_washer + 2*3;

// Thickness of horns
t_horns = 2;

// Height of horns 
h_horns = 30;

// Width of PC stem
w_pc_stem = 2;

// Diameter of hub holes (M4)
D_hub_hole = 4.25;

// Washer diameter
D_hub_washer = 15 + 0.5;

// Width of hub horns
w_hub_horns = D_hub_washer + 2*3;

// Distance between hub holes
d_hub_holes = 20;

// Height of hub holes
h_hub_holes = 100.5/2;

// Upper width of dovetail
w_dovetail_upper = 23;

// Lower width of dovetail
w_dovetail_lower = 33;

// Height of dovetail
h_dovetail = 10;

// Dovetail clearance
clearance = 2;

// Dovetail deck (thickness of horns)
deck = t_horns;

// Width between devices
w = 60;

// Length of dovetail
l_dovetail = d_pc_holes + w_pc_horns;

// Shield extra width/height
extra_shield = 5;

// Shield width
w_shield = w + 2*24.5 + 2*extra_shield;

// Shield height
h_shield = 2*h_hub_holes + 2*extra_shield;

// Distance of shield from the mount
d_shield = 50;

// Shield thickness
t_shield = 1;

// Rib thickness
t_rib = 1;

// Cone height
h_cone = 15;

// Cone diameter
D_cone = 2*h_cone;

// Cone thickness
t_cone = 0.25;

// Solar hole
D_solar_hole = 1.5;

// PC dimensions
PC = [19, 131, 82];

// Hub dimensions
hub = [24.5, 87.5, 100.5];

// Dovetail
module dovetail(l=0){
  rotate([90, 0, 0])
  translate([0, -h_dovetail - clearance])
  linear_extrude(l){
    polygon([[w_dovetail_lower/2, 0], [w_dovetail_upper/2, h_dovetail], [w_dovetail_upper/2, h_dovetail + clearance + deck], [-w_dovetail_upper/2, h_dovetail + clearance + deck], [-w_dovetail_upper/2, h_dovetail], [-w_dovetail_lower/2, 0]]);
  }
}
*dovetail();

// Horn
module horn(l, h, t, w, D, d=0, head=0){
  translate([0, 0, -w/2])
  difference(){
    union(){
      linear_extrude(w){
        square([l - 2*t, t]);
        #translate([l - t, 2*t]) square([t, h + w/2 - 2*t - t/2]);
      }
      translate([l - 2*t, 2*t]) rotate([0, 0, -90]) rotate_extrude(angle=90) polygon([[t, 0], [2*t, 0], [2*t, w], [t, w]]);
      //translate([l, h, w/2]) rotate([0, -90, 0]) cylinder(d=w, h=t);
      hull(){
        translate([t/2, t, 0]) cylinder(d=t, h=w);
        translate([l - t/2, h - w/2 + t/2, 0]) cylinder(d=t, h=w);
      }
      hull(){
        translate([w_dovetail_upper - t/2, t, 0]) cylinder(d=t, h=w);
        translate([l - t/2, h + w/2 - t/2, 0]) cylinder(d=t, h=w);
      }
      hull(){
        translate([w_dovetail_upper - t/2, t, 0]) cylinder(d=t, h=w);
        translate([l - t/2, h/2 - t/2, 0]) cylinder(d=t, h=w);
      }
    }
    translate([l, h, w/2]) rotate([0, -90, 0]) translate([0, 0, -0.01]) cylinder(d=D, h=t + 0.02);
    translate([l, h, w/2]) rotate([0, -90, 0]) translate([0, 0, t]) cylinder(d=head, h=l + 0.02);
    if( d ){
      translate([l, h - d, w/2]) rotate([0, -90, 0]) translate([0, 0, -0.01]) cylinder(d=D, h=t + 0.02);
      translate([l, h - d, w/2]) rotate([0, -90, 0]) translate([0, 0, t]) cylinder(d=head, h=l + 0.02);
    }
  }
}
*horn(w/2, 50, t_horns, 20, D_pc_hole, 20, 10);

// All horns and ruter plate
module horns(){
  for( y = [-w_pc_horns/2, -l_dovetail + w_pc_horns/2] ){
    translate([-w_dovetail_upper/2, y, 0]) rotate([90, 0, 0])
    horn(w/2 + w_dovetail_upper/2, h_pc_holes - h_dovetail - clearance, t_horns, w_pc_horns, D_pc_hole, head=D_pc_washer);
  }
  translate([w_dovetail_upper/2, -l_dovetail/2 + (PC[1] - hub[1])/2,  0]) rotate([90, 0, 180]) 
  horn(w/2 + w_dovetail_upper/2, h_hub_holes + d_hub_holes/2 - h_dovetail - clearance, t_horns, w_hub_horns, D_hub_hole, d=d_hub_holes, head=D_hub_washer);

  hull(){
    translate([-w_dovetail_upper/2, -l_dovetail, 0]) cube([w_dovetail_upper, 5, deck]);
    translate([-15, -l_dovetail - w_pc_horns/2, 10]) cube([30, 5, 30]);
  }
}

// Connecting screws
module connecting_screws(){
  for( x = [-6, 6] ){
    translate([x, -5, -6]) rotate([-90, 0, 0]) clamp_912_562(M=4, gap=12, extra_length=1, nut_centered=true, extra_bolt_height=d_shield);
  }
}

module mount(){
  ys = [-48];
  difference(){
    union(){
      dovetail(l_dovetail);
      horns();
    }
    for( y = ys ){
      translate([0, y, -h_dovetail + 2.6]) clamp_912_562(M=5, gap=15, extra_length=2.3, nut_centered=true, pyramid_nut_support=true, nut_support=0.6);
    }
    connecting_screws();    
  }
  %for( y = ys ){
    translate([0, y, deck]) cylinder(d=10, h=7.5);
    translate([0, y, deck + 7.5]) cylinder(d=20, h=4);
  }
}
mount();

module devices(){
  translate([w/2, -131/2 - l_dovetail/2, -h_dovetail - clearance]) cube(PC);
  translate([-w/2 - 24.5, -87.5/2 - l_dovetail/2 + (PC[1] - hub[1])/2, -h_dovetail - clearance]) cube(hub);
}
devices();

module shield(){
  xr = w_shield/2 - t_rib/2;
  xm = 0;
  xl = -w_shield/2 + t_rib/2;
  zb = -h_dovetail - clearance - extra_shield + t_rib/2;
  zt = zb + h_shield - t_rib;
  zm = (zb + zt)/2;
  difference(){
    union(){
      translate([0, d_shield, 0]) dovetail(d_shield);
      translate([0, d_shield, 0]) hull(){
        for( xz = [[xl, zb], [xl, zt], [xr, zb], [xr, zt]] ){
          translate([xz[0], t_shield, xz[1]]) rotate([90, 0, 0]) cylinder(d=t_rib, h=t_shield);
        }
      };
      for( xz = [[xl, zb], [xl, zm], [xl, zt], [0, zt], [xr, zb], [xr, zm], [xr, zt]] ){
        hull(){
          translate([xz[0], d_shield, xz[1]]) rotate([90, 0, 0]) cylinder(d=t_rib, h=1);
          translate([0, d_shield, -h_dovetail/2 - clearance/2]) rotate([90, 0, 0]) cylinder(d=t_rib, h=20);
        }
      }
      translate([0, d_shield, zm]) rotate([90, 0, 0]) cylinder(d1=D_cone, d2=0, h=h_cone);
    }
    alpha = atan(h_cone/(D_cone/2));
    translate([0, d_shield, zm]) rotate([90, 0, 0]) cylinder(d1=D_cone - 2*t_cone/sin(alpha), d2=0, h=h_cone - t_cone/cos(alpha));
    translate([0, d_shield + t_shield + 0.01, zm]) rotate([90, 0, 0]) cylinder(d=D_solar_hole, h=t_shield + 0.02);
    connecting_screws();
  }
}
!shield();