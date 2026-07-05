$fn = $preview ? 36 : 360;
use <cutouts/screws.scad>

// Distance between PC holes
d_pc_holes = 80;

// Height of PC holes
h_pc_holes = 47;

// Hole diameter (M3)
D_pc_hole = 3.25;

// Width of horns
w_horns = 10;

// Thickness of horns
t_horns = 5;

// Height of horns 
h_horns = 30;

// Width of PC stem
w_pc_stem = 2;

// Diameter of hub holes (M4)
D_hub_hole = 4.25;

// Distance between hub holes
d_hub_holes = 20;

// Height of hub holes
h_hub_holes = 100.5/2;

// Upper width of dovetail
w_dovetail_upper = 20;

// Lower width of dovetail
w_dovetail_lower = 32;

// Height of dovetail
h_dovetail = 10;

// Dovetail clearance
clearance = 1;

// Dovetail deck (thickness of horns)
deck = t_horns;

// Width between devices
w = 60;

// Length of dovetail
l_dovetail = d_pc_holes + w_horns;
echo(l_dovetail);

// Dovetail
module dovetail(){
  rotate([90, 0, 0])
  translate([0, -h_dovetail - clearance])
  linear_extrude(l_dovetail){
    polygon([[w_dovetail_lower/2, 0], [w_dovetail_upper/2, h_dovetail], [w_dovetail_upper/2, h_dovetail + clearance + deck], [-w_dovetail_upper/2, h_dovetail + clearance + deck], [-w_dovetail_upper/2, h_dovetail], [-w_dovetail_lower/2, 0]]);
  }
}
*dovetail();

// Horn
module horn(l, h, t, w, D, d=0){
  difference(){
    union(){
      linear_extrude(w){
        square([l - 2*t, t]);
        translate([l - t, 2*t]) square([t, h - 2*t]);
      }
      translate([l - 2*t, 2*t]) rotate([0, 0, -90]) rotate_extrude(angle=90) polygon([[t, 0], [2*t, 0], [2*t, w], [t, w]]);
      translate([l, h, w/2]) rotate([0, -90, 0]) cylinder(d=w, h=t);
    }
    translate([l, h, w/2]) rotate([0, -90, 0]) translate([0, 0, -0.01]) cylinder(d=D, h=t + 0.02);
    if( d ){
      translate([l, h - d, w/2]) rotate([0, -90, 0]) translate([0, 0, -0.01]) cylinder(d=D, h=t + 0.02);
    }
  }
}
*horn(w/2, 50, t_horns, 10, D_pc_hole);

// All horns and ruter plate
module horns(){
  for( y = [0, -l_dovetail + w_horns] ){
    translate([0, y, 0]) rotate([90, 0, 0]) horn(w/2, h_pc_holes - h_dovetail - clearance, t_horns, w_horns, D_pc_hole);
  }
  translate([0, -l_dovetail/2 - w_horns/2, 0]) rotate([90, 0, 180]) horn(w/2, h_hub_holes + d_hub_holes/2 - h_dovetail - clearance, t_horns, w_horns, D_hub_hole, d_hub_holes);

  hull(){
    translate([-w_dovetail_upper/2, -l_dovetail, 0]) cube([w_dovetail_upper, 5, deck]);
    translate([-15, -l_dovetail - w_horns/2, 10]) cube([30, 5, 30]);
  }
}

difference(){
  union(){
    dovetail();
    horns();
  }
  for( y = [-20, -45, -70] ){
    translate([0, y, -h_dovetail + 2.6]) clamp_912_562(M=5, gap=15, extra_length=2.3, nut_centered=true, pyramid_nut_support=true, nut_support=0.6);
  }
  for( x = [-7, 7] ){
    translate([x, -5, -6]) rotate([-90, 0, 0]) clamp_912_562(M=4, gap=5, extra_length=1, nut_centered=true);
  }
}