$fn = $preview ? 90 : 360;

// Diameter of dew shield
D_shield = 84;

// Ring thickness
t_ring = 4*0.45;

// Outer ring diameter
D_ring = D_shield + 2*t_ring;

// Length of flange
l_flange = 33;

// Flange height
h_flange = 11;

// Nut height
h_nut = 2;

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

// Flange Y
y_flange = sqrt((D_ring/2)^2 - (d_flange/2 - h_nut)^2);

// Bolt collar thickness
t_collar = 2;

// Clamp gap on each side
gap = 0.5;

// Bolt diameter + tolerance
D_bolt = 3 + 0.25;

// Bolt collar diameter + tolerance
D_bolt_collar = 5.5 + 1;

// Thumbscrew collar diameter
D_thumbscrew_collar = 6 + 1;

// Nut width
w_nut = 5.5;

// Basic ring
module ring(){
  translate([0, 0, -l_flange/2])
  linear_extrude(l_flange, convexity=4)
  {
    difference(){
      hull(){
        circle(d=D_ring);
        translate([-w_flange/2 - t_collar - gap, y_flange]) square([w_flange + 2*t_collar + 2*gap, h_flange]);
      }
      circle(d=D_shield);
      translate([-d_flange/2 + h_nut, 0]) square([d_flange - 2*h_nut, D_shield + 2*t_ring + 0.01]);
      for( x = [-d_flange/2 - t_flange - gap, d_flange/2 - gap] ){
        translate([x, y_flange]) square([t_flange + 2*gap, h_flange]);
      }
    }
  }
}
*ring();

// Ring with cutouts
module final(thumbscrews=false){
  difference(){
    ring();
    for( sgn_z = [-1, 1] ){
      z = sgn_z*(l_slit/2 - h_slit/2);
      y = y_flange + d_slit + h_slit/2;
      x_o = w_flange/2 + t_collar + gap;
      x_i = d_flange/2;
      h_nut_slit = h_slit + d_slit + 5.5/2;
      translate([0, y, z]){
        rotate([0, 90, 0]) cylinder(d=D_bolt, h=2*x_o + 0.02, center=true);
      }
      for( sgn_x = [-1, 1] ){
        hull(){
          for( pm = [-1, 1] ){
            translate([sgn_x*x_o, y + pm, z]) rotate([0, sgn_x*90, 0]) cylinder(d=thumbscrews && sgn_x == 1 ? D_thumbscrew_collar : D_bolt_collar, h=D_bolt_collar);
          }
        }
        translate([sgn_x*x_i, y_flange, z]) rotate([0, -sgn_x*90, 0]) hull(){
          translate([-w_nut/2, 0, -0.01]) cube([w_nut, h_nut_slit, 0.01]);
          translate([-w_nut/2 - h_nut/2, 0, h_nut]) cube([w_nut + h_nut, h_nut_slit, 0.01]);
        }
      }
    }
  }
  %for( sgn_z = [-1, 1] ){
    z = sgn_z*(l_slit/2 - h_slit/2);
    y = y_flange + d_slit + h_slit/2;
    x_o = w_flange/2 + t_collar + gap;
    for( sgn_x = [-1, 1] ){
      translate([sgn_x*x_o, y, z]) 
      rotate([0, sgn_x*90, 0])
      if( thumbscrews && sgn_x == 1 ){
        translate([0, 0, -16]) cylinder(d=3, h=16);
        cylinder(d=6, h=7.5 - 2.5);
        translate([0, 0, 7.5 - 2.5]) cylinder(d=12, h=2.5);
      }
      else{
        translate([0, 0, -16]) cylinder(d=3, h=16);
        cylinder(d=5.5, h=2);
      }
    }
  }
}
final(true);
    
    