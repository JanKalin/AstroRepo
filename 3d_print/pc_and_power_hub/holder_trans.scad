$fn = $preview ? 36 : 360;

// Distance between PC holes
d_pc_holes = 80;

// Height of PC holes
h_pc_holes = 47;

// Hole diameter (M4)
D_hole = 4.5;

// Width of horns
w_horns = D_hole + 2*3;

// Thickness of horns
t_horns = 5;

// Height of horns 
h_horns = 30;

// Width of PC stem
w_pc_stem = 2;

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
clearance = 10;

// Length of dovetail
l_dovetail = 40;

// Horns
module horns(){
  linear_extrude(t_horns){
    for( x = [-d_pc_holes, d_pc_holes] ){
      difference(){
        hull(){
          translate([x, h_pc_holes]) circle(d=w_horns);
          translate([x, h_horns]) circle(d=w_horns);
        }
        translate([x, h_pc_holes]) circle(d=D_hole);
      }
      hull(){
        translate([x, h_horns]) circle(d=w_horns);
        translate([0, h_horns]) circle(d=w_horns);
      }
    }
    translate([-w_pc_stem/2, 0]) square([w_pc_stem, h_horns]);
  }
}
horns();

// Dovetail
module dovetail(){
  translate([0, -h_dovetail - clearance])
  linear_extrude(l_dovetail){
    polygon([[w_dovetail_lower/2, 0], [w_dovetail_upper/2, h_dovetail], [w_dovetail_upper/2, h_dovetail + clearance], [-w_dovetail_upper/2, h_dovetail + clearance], [-w_dovetail_upper/2, h_dovetail], [-w_dovetail_lower/2, 0]]);
  }
}
dovetail();

// Hub plate
module hub_plate(){
  translate([0, 0, l_dovetail - t_horns])
  linear_extrude(t_horns){
    difference(){
      hull(){
        for( x = [-d_hub_holes/2, d_hub_holes/2], y = [w_horns/2, h_hub_holes] ){
          translate([x, y]) circle(d=w_horns);
        }
      }
      for( x = [-d_hub_holes/2, d_hub_holes/2] ){
        translate([x, h_hub_holes]) circle(d=D_hole);
      }
    }
  }
}
hub_plate();

// Connector
module connector(){
  hull(){
    for( x = [-d_hub_holes/2, d_hub_holes/2] ){
      translate([x, h_horns]) cylinder(d=w_horns, h=l_dovetail);
    }
  }
}
connector();