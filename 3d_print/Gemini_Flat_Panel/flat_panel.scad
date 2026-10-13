$fn = $preview ? 180 : 360;

// Diameter of dew shield
D_shield = 84;

// Ring thickness
t_ring = 4*0.45;

// Outer ring diameter
D_ring = D_shield + 2*t_ring;

// Box width
w_box = 65.5;

// box height
h_box = 49;

// Length of flange
l_flange = 33;

// Total flange height
h_flange_total = 12;

// Live flange height
h_flange_live = 10;

// Flange slit height
h_slit = 3.5;

// Distance of slit to flange bottom
d_slit = 3;

// Length of slit
l_slit = 23.5;

// Distance between flanges
d_flange = 37;

// Flange thickness 
t_flange = 9;

// Outer width of flanges
w_flange = d_flange + 2*t_flange;

// Delta Y
delta_y = 2.75;

// Flange Y
y_flange = sqrt((D_ring/2)^2 - (d_flange/2)^2) + delta_y;

// Bolt collar thickness
t_collar = 2;

// Nut height
h_nut = 3;

// Outer gap
gap_o = 0.25;

// Inner gap
gap_i = 0.75;

// Bolt diameter + tolerance
D_bolt = 3 + 0.25;

// Bolt collar diameter + tolerance
D_bolt_collar = 5.5 + 1;

// Thumbscrew collar diameter
D_thumbscrew_collar = 6 + 1;

// Nut width
w_nut = 5.5;

// Panel diameter
D_panel = 127;

// Panel slit diameter
D_panel_slit = (79.7 - 72.3)/2;

// Panel slit x
x_panel_slit = (72.3 + 79.7)/2/2;

// Panel lower slit y
y_panel_l = sqrt(69^2 - x_panel_slit^2);

// Panel upper slit y
y_panel_u = sqrt(80^2 - x_panel_slit^2);

// Plate plate thickness
t_panel_plate = 1;

// Plate plate and bolts thickness
t_panel_plate_and_bolts = 2;

// Panel thickness (1mm are the screws)
t_panel_panel = 8.5 - t_panel_plate + 1;

// Slit lip
panel_lip = 10;

// Panel height above the box
h_panel = 21;

// Distance of centre between panel mounting screws to panel flange bottom
d_panel_mount = (25 + 18)/2;

// Catch depth
d_catch = 1;

// Distance of catch from the edge
d_catch2shield = 2;

// Distance between ring and edge of dew shield
d_ring2shield = h_panel - t_panel_panel + t_panel_plate;

// Hook angle
a_hook = 16;

// Bed skirt tolerance for panel diameter
tol_bed_panel_D = 1;

// Bed tolerance for shield diameter
tol_bed_shield_D = 0.25;

// Bed tolerance for ring diameter
tol_bed_ring_D = 0.1;

// Height of bed skirt
h_bed_skirt = t_panel_panel - 1;

// Thickness of bed skirt
t_bed_skirt = 3*0.45;

// Opening of the skirt
opening_bed_skirt = 15;



// Hooks for the bed
module hook(a=a_hook){
  rotate([0, 0, -a/2])
  rotate_extrude(angle=a, convexity=4){
    translate([D_shield/2, 0])
    translate([0, -t_panel_plate_and_bolts])
    polygon([[0, t_panel_plate_and_bolts], [t_ring, t_panel_plate_and_bolts], [t_ring, d_ring2shield - 2*d_catch - d_catch2shield - t_ring], [t_ring - d_catch, d_ring2shield - 1*d_catch - d_catch2shield - t_ring], [t_ring, d_ring2shield - d_catch2shield - t_ring], [d_catch, d_ring2shield - d_catch2shield - d_catch], [0, d_ring2shield - d_catch2shield - d_catch]]);
  }
}
*hook();

module hooks(){
  for( a = [-90, 30, 150] ){
    rotate([0, 0, a]) translate([0, 0, l_flange/2])
    hook();
  }
}
*hooks();

// Bed in which the panel lays down to rest
module bed(){
  fillet = 0.33;
  rotate([0, 0, 45])
  translate([0, 0, l_flange/2 + d_ring2shield])
  difference(){
    rotate_extrude(a=360, convexity=4)
    translate([0, -t_panel_plate_and_bolts])
    polygon([
    [D_ring/2 + tol_bed_ring_D, -d_catch2shield - t_ring - 2*d_catch],
    [D_panel/2 + tol_bed_panel_D + t_bed_skirt - fillet, -d_catch2shield - t_ring - 2*d_catch],
    [D_panel/2 + tol_bed_panel_D + t_bed_skirt, -d_catch2shield - t_ring - 2*d_catch + fillet],
    [D_panel/2 + tol_bed_panel_D + t_bed_skirt, h_bed_skirt - fillet],
    [D_panel/2 + tol_bed_panel_D + t_bed_skirt - fillet, h_bed_skirt],
    [D_panel/2 + tol_bed_panel_D + fillet, h_bed_skirt],
    [D_panel/2 + tol_bed_panel_D, h_bed_skirt - fillet],
    [D_panel/2 + tol_bed_panel_D, 0],
    [D_shield/2 + tol_bed_shield_D + 1, 0],
    [D_shield/2 + tol_bed_shield_D + 1, t_panel_plate_and_bolts],
    [D_shield/2 + tol_bed_shield_D, t_panel_plate_and_bolts],
    [D_shield/2 + tol_bed_shield_D, 0],
    [D_shield/2 + tol_bed_shield_D, -d_catch2shield],
    [D_ring/2 + tol_bed_ring_D, -d_catch2shield - t_ring],
    [D_ring/2 + tol_bed_ring_D - d_catch, -d_catch2shield - t_ring - d_catch],
    [D_ring/2 + tol_bed_ring_D, -d_catch2shield - t_ring - 2*d_catch]]);
    translate([-opening_bed_skirt/2, D_panel/2, -t_panel_plate_and_bolts + 0.001]) cube([opening_bed_skirt, 3, h_bed_skirt]);
  }
}
*bed();

// Basic ring
module ring(){
  c = 2;
  translate([0, 0, -l_flange/2])
  linear_extrude(l_flange, convexity=4)
  {
    difference(){
      hull(){
        circle(d=D_ring);
        translate([-w_flange/2 - t_collar + c, y_flange + h_flange_live - c]) circle(r=c);
        translate([w_flange/2 + t_collar + gap_o - c, y_flange + h_flange_live - c]) circle(r=c);
      }
      circle(d=D_shield);
      translate([-d_flange/2 + h_nut, sqrt((D_shield/2)^2 - (d_flange/2 - h_nut - gap_i)^2)]) square([d_flange - 2*h_nut - gap_i, D_shield + 2*t_ring + 0.01]);

      translate([-d_flange/2 + (h_nut - c), sqrt((D_shield/2)^2 - (d_flange/2 - (h_nut - c))^2)])
      translate([0, c])
      rotate([0, 0, -90])
      difference(){
        square([c, 2*c]);
        circle(c);
      }

      mirror([1, 0, 0])
      translate([-d_flange/2 + (h_nut - c) + gap_i, sqrt((D_shield/2)^2 - (d_flange/2 - gap_i - (h_nut - c))^2)])
      translate([0, c])
      rotate([0, 0, -90])
      difference(){
        square([c, 2*c]);
        circle(c);
      }
      
      translate([-d_flange/2 - t_flange, y_flange]) square([t_flange, h_flange_total + 0.01]);
      translate([d_flange/2 - gap_i, y_flange]) square([t_flange + gap_o + gap_i, h_flange_total + 0.01]);
    }
  }
}
*ring();

// Ring with cutouts
module main(add_hooks=true){
  y = y_flange + d_slit + 3/2 + 0.1;
  x_o = w_flange/2 + t_collar + gap_o;
  x_i = d_flange/2 - gap_i;
  //color("red", 1)
  difference(){
    ring();
    translate([0, 0, -l_flange/2 - 0.01]) cylinder(d1=D_shield + 2, d2=D_shield, h=1);
    for( sgn_z = [-1, 1] ){
      z = sgn_z*(l_slit/2 - h_slit/2);
      hull(){
        translate([0, y - 1, z]){
          rotate([0, 90, 0]) cylinder(d=D_bolt, h=2*x_o + 0.02, center=true);
        }
        translate([0, y + 1, z]){
          rotate([0, 90, 0]) cylinder(d=D_bolt, h=2*x_o + 0.02, center=true);
        }
      }
      hull(){
        translate([-w_flange/2 - t_collar, y - 1, z]) rotate([0, -90, 0]) cylinder(d=D_bolt_collar, h=D_bolt_collar);
        translate([-w_flange/2 - t_collar, y + 1, z]) rotate([0, -90, 0]) cylinder(d=D_bolt_collar, h=D_bolt_collar);
      }
      hull(){
        translate([w_flange/2 + t_collar + gap_o, y - 1, z]) rotate([0, 90, 0]) cylinder(d=D_thumbscrew_collar, h=D_bolt_collar);
        translate([w_flange/2 + t_collar + gap_o, y + 1, z]) rotate([0, 90, 0]) cylinder(d=D_thumbscrew_collar, h=D_bolt_collar);
      }
      hull(){
        translate([-d_flange/2, y - 1, z]) rotate([0, 90, 0]) translate([-w_nut/2 - 0.25, -w_nut/2, -0.01]) cube([w_nut + 0.5, w_nut, h_nut + 0.02]);
        translate([-d_flange/2, y + 1, z]) rotate([0, 90, 0]) translate([-w_nut/2 - 0.25, -w_nut/2, -0.01]) cube([w_nut + 0.5, w_nut, h_nut + 0.02]);
      }
      hull(){
        translate([d_flange/2 - gap_i, y - 1, z]) rotate([0, -90, 0]) translate([-w_nut/2 - 0.25, -w_nut/2, -0.01]) cube([w_nut + 0.5, w_nut, h_nut + 0.02]);
        translate([d_flange/2 - gap_i, y + 1, z]) rotate([0, -90, 0]) translate([-w_nut/2 - 0.25, -w_nut/2, -0.01]) cube([w_nut + 0.5, w_nut, h_nut + 0.02]);
      }
    }
  }
  if( add_hooks ){
    hooks();
  }
  %for( sgn_z = [-1, 1] ){
    z = sgn_z*(l_slit/2 - h_slit/2);
    for( sgn_x = [-1, 1] ){
      translate([sgn_x*x_o, y, z]) 
      rotate([0, sgn_x*90, 0])
      if( sgn_x == 1 ){
        translate([0, 0, -16]) cylinder(d=3, h=16);
        cylinder(d=6, h=7.5 - 2.5);
        translate([0, 0, 7.5 - 2.5]) cylinder(d=12, h=2.5);
      }
      else{
        translate([0, 0, -16]) cylinder(d=3, h=16);
        cylinder(d=5.5, h=4);
      }
    }
  }
  //circle(d=D_shield - 0.2);
}
*main();

// Device
module box(){
  color("grey", 0.25)
  translate([0, y_flange, -l_flange/2]){
    linear_extrude(l_flange, convexity=4)
    polygon([[-w_flange/2, 0], [-w_flange/2, h_flange_total], [-w_box/2, h_flange_total], [-w_box/2, h_box], [w_box/2, h_box], [w_box/2, h_flange_total], [w_flange/2, h_flange_total], [w_flange/2, 0], [d_flange/2, 0], [d_flange/2, h_flange_total], [-d_flange/2, h_flange_total], [-d_flange/2, 0]]);
  }
}
*box();
 
// Panel
module panel(){
  translate([0, -(y_panel_u + y_panel_l)/2 + y_flange + d_panel_mount, l_flange/2 + h_panel + t_panel_plate]){
    rotate([0, 180, 0]){
      color("green", 0.25){
        cylinder(d=D_panel, h=t_panel_panel);
        for( sgn_x = [-1, 1] ){
          translate([sgn_x*x_panel_slit, 0])
          difference(){
            hull(){
              cylinder(d=D_panel_slit + 2*panel_lip, h=t_panel_plate);
              translate([0, y_panel_u, 0]) cylinder(d=D_panel_slit + 2*panel_lip, h=t_panel_plate);
            }
            hull(){
              translate([0, y_panel_l, -0.01]) cylinder(d=D_panel_slit, h=t_panel_plate + 0.02);
              translate([0, y_panel_u, -0.01]) cylinder(d=D_panel_slit, h=t_panel_plate + 0.02);
            }
          }
        }
      }
    }
    color("green") translate([0, 0, -l_flange/2 - h_panel - t_panel_plate]) cylinder(d=1, h=l_flange/2 + h_panel + t_panel_plate);
  }
}
*panel();
    
// Lens
module lens(){
  color("red")
  translate([0, 0, -l_flange/2])
  difference(){
    cylinder(d=D_shield, h=l_flange + d_ring2shield);
    translate([0, 0, -0.01]) cylinder(d=D_shield - 2, h=l_flange + d_ring2shield + 0.02);    
  }
}
*lens();


// 0: all; 1: main, 2: bed and hooks

what = 0
;

if( what == 0 ){
  main(true);
//  box();
//  panel();
  lens();
  bed();
}
if( what == 1 ){
  main();
}
if( what == 2 ){
  %hooks();
  bed();
}