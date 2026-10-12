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

// Flange height
h_flange = 12;

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
h_nut = 2;

// Outer gap
gap_o = 0.5;

// Inner gap
gap_i = 1.5;

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
D_panel_slit = (79.7 - 72.3);

// Panel slit x
x_panel_slit = (72.3 + 79.7)/2/2;

// Panel lower slit y
y_panel_l = sqrt(69^2 - x_panel_slit^2);

// Panel upper slit y
y_panel_u = sqrt(80^2 - x_panel_slit^2);

// Plate plate thickness
t_panel_plate = 1;

// Panel thickness
t_panel_panel = 8.5;

// Slit lip
panel_lip = 5;

// Panel height above the box
h_panel = 21;

// Distance of centre between panel mounting screws to panel flange bottom
d_panel_mount = (25 + 18)/2;

// Basic ring
module ring(){
  c = 2;
  translate([0, 0, -l_flange/2])
  linear_extrude(l_flange, convexity=4)
  {
    difference(){
      hull(){
        circle(d=D_ring);
        translate([-w_flange/2 - t_collar + c, y_flange + h_flange - c]) circle(r=c);
        translate([w_flange/2 + t_collar + gap_o - c, y_flange + h_flange - c]) circle(r=c);
      }
      circle(d=D_shield);
      translate([-d_flange/2 + h_nut, sqrt((D_shield/2)^2 - (d_flange/2 - h_nut - gap_i)^2)]) square([d_flange - 2*h_nut - gap_i, D_shield + 2*t_ring + 0.01]);

      translate([-d_flange/2, sqrt((D_shield/2)^2 - (d_flange/2)^2)])
      translate([0, c])
      rotate([0, 0, -90])
      difference(){
        square([h_nut, 2*h_nut]);
        circle(h_nut);
      }

      mirror([1, 0, 0])
      translate([-d_flange/2 + gap_i, sqrt((D_shield/2)^2 - (d_flange/2 - gap_i)^2)])
      translate([0, c])
      rotate([0, 0, -90])
      difference(){
        square([h_nut, 2*h_nut]);
        circle(h_nut);
      }
      
      translate([-d_flange/2 - t_flange, y_flange]) square([t_flange, h_flange + 0.01]);
      translate([d_flange/2 - gap_i, y_flange]) square([t_flange + gap_o + gap_i, h_flange + 0.01]);
    }
  }
}
*ring();

// Ring with cutouts
module final(){
  y = y_flange + d_slit + 3/2 + 0.1;
  x_o = w_flange/2 + t_collar + gap_o;
  x_i = d_flange/2 - gap_i;
  //color("red", 1)
  difference(){
    ring();
    for( sgn_z = [-1, 1] ){
      z = sgn_z*(l_slit/2 - h_slit/2);
      h_nut_slit = w_nut;
      translate([0, y, z]){
        rotate([0, 90, 0]) cylinder(d=D_bolt, h=2*x_o + 0.02, center=true);
      }
      translate([-w_flange/2 - t_collar, y, z]) rotate([0, -90, 0]) cylinder(d=D_bolt_collar, h=D_bolt_collar);
      translate([w_flange/2 + t_collar + gap_o, y, z]) rotate([0, 90, 0]) cylinder(d=D_thumbscrew_collar, h=D_bolt_collar);
      translate([-d_flange/2, y, z]) rotate([0, 90, 0]) translate([-w_nut/2, -w_nut/2, -0.01]) cube([w_nut, h_nut_slit, h_nut + 0.02]);
      translate([d_flange/2 - h_nut + gap_o, y, z]) rotate([0, -90, 0]) translate([-w_nut/2, -w_nut/2, -0.01]) cube([w_nut, h_nut_slit, h_nut + 0.02]);
    }
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
final();

// Device
module box(){
  color("grey", 0.25)
  translate([0, y_flange, -l_flange/2]){
    linear_extrude(l_flange, convexity=4) polygon([[-w_flange/2, 0], [-w_flange/2, h_flange + 1], [-w_box/2, h_flange + 1], [-w_box/2, h_box], [w_box/2, h_box], [w_box/2, h_flange + 1], [w_flange/2, h_flange + 1], [w_flange/2, 0], [d_flange/2, 0], [d_flange/2, h_flange + 1], [-d_flange/2, h_flange + 1], [-d_flange/2, 0]]);
  }
}
%box();
 
// Panel
module panel(){
  translate([0, -(y_panel_u + y_panel_l)/2 + y_flange + d_panel_mount, l_flange/2 + h_panel]){
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
    color("green") translate([0, 0, -l_flange/2 - h_panel]) cylinder(d=1, h=l_flange/2 + h_panel);
  }
}
%panel();
    